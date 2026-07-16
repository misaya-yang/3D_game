using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.Quest;

namespace Wendao.Systems.NPC
{
    public sealed class DialogueManager : SafeBehaviour, IDialogueService
    {
        private readonly List<DialogueChoice> _visibleChoices =
            new List<DialogueChoice>();

        private ReadOnlyCollection<DialogueChoice> _readOnlyVisibleChoices;
        private DialogueData _currentDialogue;
        private IPlayerInputSource _input;
        private bool _registeredService;
        private bool _inputSuspended;
        private bool _inputWasEnabled;
        private bool _nodeActionsApplied;

        public bool IsOpen => _currentDialogue != null && CurrentNode != null;
        public string CurrentDialogueId => _currentDialogue?.Id ?? string.Empty;
        public string CurrentNpcId { get; private set; } = string.Empty;
        public DialogueNode CurrentNode { get; private set; }
        public IReadOnlyList<DialogueChoice> VisibleChoices =>
            _readOnlyVisibleChoices;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IDialogueService>(
                    out IDialogueService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyVisibleChoices = _visibleChoices.AsReadOnly();
            ServiceLocator.Register<IDialogueService>(this);
            _registeredService = true;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<IDialogueService>(
                    out IDialogueService current))
            {
                ServiceLocator.Register<IDialogueService>(this);
                _registeredService = true;
            }
            else
            {
                _registeredService = ReferenceEquals(current, this);
            }

            if (IsOpen && !_inputSuspended)
            {
                SuspendGameplayInput();
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                EndDialogue(true);
            }
            else
            {
                RestoreGameplayInput();
            }
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            RestoreGameplayInput();
            if (_registeredService
                && ServiceLocator.TryGet<IDialogueService>(
                    out IDialogueService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IDialogueService>();
            }

            _registeredService = false;
        }

        public void StartDialogue(string dialogueId, string npcId)
        {
            TryStartDialogue(dialogueId, npcId);
        }

        public bool TryStartDialogue(string dialogueId, string npcId)
        {
            DialogueData dialogue = ConfigDatabase.Instance?.GetDialogue(dialogueId);
            NPCData npc = ConfigDatabase.Instance?.GetNpc(npcId);
            GameManager gameManager = GameManager.Instance;
            if (IsOpen
                || dialogue?.Nodes == null
                || dialogue.Nodes.Length == 0
                || npc == null
                || gameManager == null
                || gameManager.State != GameState.Playing
                || !gameManager.TrySetState(GameState.Dialogue))
            {
                return false;
            }

            _currentDialogue = dialogue;
            CurrentNpcId = npc.Id;
            CurrentNode = dialogue.Nodes[0];
            _nodeActionsApplied = false;
            RebuildVisibleChoices();
            SuspendGameplayInput();
            EventBus.Publish(
                DialogueEvents.Started,
                new DialogueInfo
                {
                    NpcId = CurrentNpcId,
                    DialogueId = CurrentDialogueId,
                    Cancelled = false
                });
            return true;
        }

        public void Advance()
        {
            if (!IsOpen || _visibleChoices.Count > 0)
            {
                return;
            }

            if (CurrentNode.End || string.IsNullOrEmpty(CurrentNode.NextNodeId))
            {
                FinishCurrentNode();
                return;
            }

            if (!MoveToNode(CurrentNode.NextNodeId))
            {
                EndDialogue(true);
            }
        }

        public void Choose(int choiceIndex)
        {
            if (!IsOpen
                || choiceIndex < 0
                || choiceIndex >= _visibleChoices.Count)
            {
                return;
            }

            DialogueChoice choice = _visibleChoices[choiceIndex];
            ApplyChoiceFlag(choice);
            if (string.IsNullOrEmpty(choice.NextNodeId))
            {
                EndDialogue(false);
                return;
            }

            if (!MoveToNode(choice.NextNodeId))
            {
                EndDialogue(true);
            }
        }

        public void EndDialogue(bool cancelled)
        {
            if (!IsOpen)
            {
                return;
            }

            if (!cancelled)
            {
                ApplyNodeActions();
            }

            var info = new DialogueInfo
            {
                NpcId = CurrentNpcId,
                DialogueId = CurrentDialogueId,
                Cancelled = cancelled
            };
            _currentDialogue = null;
            CurrentNpcId = string.Empty;
            CurrentNode = null;
            _visibleChoices.Clear();
            _nodeActionsApplied = false;

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Dialogue)
            {
                gameManager.TrySetState(GameState.Playing);
            }

            RestoreGameplayInput();
            EventBus.Publish(DialogueEvents.Ended, info);
        }

        private void FinishCurrentNode()
        {
            ApplyNodeActions();
            EndDialogue(false);
        }

        private void ApplyNodeActions()
        {
            if (_nodeActionsApplied || CurrentNode == null)
            {
                return;
            }

            _nodeActionsApplied = true;
            if (!ServiceLocator.TryGet<IQuestService>(out IQuestService quests))
            {
                return;
            }

            if (!string.IsNullOrEmpty(CurrentNode.QuestOfferId))
            {
                quests.Accept(CurrentNode.QuestOfferId);
            }

            if (!string.IsNullOrEmpty(CurrentNode.QuestTurnInId))
            {
                QuestData quest = quests.GetQuestData(CurrentNode.QuestTurnInId);
                if (quest != null
                    && string.Equals(
                        quest.TurnInNpcId,
                        CurrentNpcId,
                        StringComparison.Ordinal))
                {
                    quests.TurnIn(CurrentNode.QuestTurnInId);
                }
            }
        }

        private bool MoveToNode(string nodeId)
        {
            DialogueNode node = FindNode(nodeId);
            if (node == null)
            {
                return false;
            }

            CurrentNode = node;
            _nodeActionsApplied = false;
            RebuildVisibleChoices();
            return true;
        }

        private DialogueNode FindNode(string nodeId)
        {
            DialogueNode[] nodes = _currentDialogue?.Nodes;
            if (nodes == null || string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            for (int index = 0; index < nodes.Length; index++)
            {
                if (nodes[index] != null
                    && string.Equals(
                        nodes[index].NodeId,
                        nodeId,
                        StringComparison.Ordinal))
                {
                    return nodes[index];
                }
            }

            return null;
        }

        private void RebuildVisibleChoices()
        {
            _visibleChoices.Clear();
            DialogueChoice[] choices = CurrentNode?.Choices
                ?? Array.Empty<DialogueChoice>();
            for (int index = 0; index < choices.Length; index++)
            {
                DialogueChoice choice = choices[index];
                if (choice == null
                    || choice.RequiredAffection > 0
                    || !MeetsQuestRequirement(choice.RequiredQuestId))
                {
                    continue;
                }

                _visibleChoices.Add(choice);
            }
        }

        private static bool MeetsQuestRequirement(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return true;
            }

            if (!ServiceLocator.TryGet<IQuestService>(out IQuestService quests))
            {
                return false;
            }

            QuestStatus status = quests.GetStatus(questId);
            return status == QuestStatus.Completed
                || status == QuestStatus.TurnedIn;
        }

        private static void ApplyChoiceFlag(DialogueChoice choice)
        {
            if (choice == null || string.IsNullOrEmpty(choice.SetFlag))
            {
                return;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager?.World?.QuestFlags == null)
            {
                return;
            }

            saveManager.World.QuestFlags[choice.SetFlag] = true;
            if (saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("world");
            }
        }

        private void SuspendGameplayInput()
        {
            if (_inputSuspended)
            {
                return;
            }

            if (!IsInputAlive())
            {
                _input = null;
            }

            if (_input == null && !ServiceLocator.TryGet(out _input))
            {
                return;
            }

            if (!IsInputAlive())
            {
                return;
            }

            _inputWasEnabled = _input.IsEnabled;
            _input.SetEnabled(false);
            _inputSuspended = true;
        }

        private void RestoreGameplayInput()
        {
            if (!_inputSuspended)
            {
                return;
            }

            if (IsInputAlive() && _inputWasEnabled)
            {
                _input.SetEnabled(true);
            }
            else if (!IsInputAlive())
            {
                _input = null;
            }

            _inputSuspended = false;
            _inputWasEnabled = false;
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (IsOpen)
            {
                EndDialogue(true);
            }
        }
    }
}
