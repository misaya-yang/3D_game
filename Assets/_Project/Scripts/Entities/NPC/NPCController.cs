using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Input;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Shop;

namespace Wendao.Entities.NPC
{
    public sealed class NPCController : SafeBehaviour, IDialogueFocusTarget
    {
        public const float InteractionDistance = 3f;
        public const string InteractionPromptLocalizationKey = "ui_npc_interact";
        public const string InteractionPromptDefaultValue = "[E] 与{0}交谈";
        public const string VendorInteractionPromptLocalizationKey =
            "ui_vendor_interact";
        public const string VendorInteractionPromptDefaultValue = "[E] 与{0}交易";

        private PlayerController _player;
        private IPlayerInputSource _input;
        private IDialogueService _dialogue;
        private IQuestService _quests;
        private IShopService _shop;
        private TextMesh _prompt;

        public NPCData Data { get; private set; }
        public int Affection { get; private set; }
        public bool IsPlayerInRange { get; private set; }
        public string CurrentPromptLocalizationKey =>
            Data != null && Data.IsVendor
                ? VendorInteractionPromptLocalizationKey
                : InteractionPromptLocalizationKey;
        public string NpcId => Data?.Id ?? string.Empty;
        public Transform DialogueFocusTransform => _prompt != null
            ? _prompt.transform
            : transform;

        private void Awake()
        {
            BuildPrompt();
            RefreshPromptText();
            SetPromptVisible(false);
        }

        private void Update()
        {
            ResolveRuntimeReferences();
            IsPlayerInRange = _player != null
                && Vector3.SqrMagnitude(
                    _player.transform.position - transform.position)
                    <= InteractionDistance * InteractionDistance;
            bool inputAlive = IsInputAlive();
            bool canInteract = IsPlayerInRange
                && inputAlive
                && _input.IsEnabled
                && CanInteractWithConfiguredRole();
            SetPromptVisible(canInteract);
            FacePromptToCamera();

            if (canInteract && _input.InteractPressedThisFrame)
            {
                Interact();
            }
        }

        public void ConfigureData(NPCData data)
        {
            Data = data;
            RefreshPromptText();
        }

        public void Interact()
        {
            TryInteract();
        }

        public bool TryInteract()
        {
            ResolveRuntimeReferences();
            bool inputAlive = IsInputAlive();
            if (Data == null
                || _player == null
                || Vector3.SqrMagnitude(
                    _player.transform.position - transform.position)
                    > InteractionDistance * InteractionDistance
                || (inputAlive && !_input.IsEnabled))
            {
                return false;
            }

            if (Data.IsVendor)
            {
                return _shop != null
                    && !_shop.IsOpen
                    && _shop.OpenVendor(Data.Id);
            }

            if (_dialogue == null || _dialogue.IsOpen)
            {
                return false;
            }

            string dialogueId = Data.DefaultDialogueId;
            if (_quests != null)
            {
                dialogueId = _quests.ResolveInteractionDialogueId(
                    Data.Id,
                    dialogueId);
            }

            return _dialogue.TryStartDialogue(dialogueId, Data.Id);
        }

        public void AddAffection(int delta)
        {
            int previous = Affection;
            long next = (long)Affection + delta;
            Affection = (int)Math.Max(0L, Math.Min(int.MaxValue, next));
            if (Affection == previous)
            {
                return;
            }

            EventBus.Publish(
                NpcEvents.AffectionChanged,
                new AffectionInfo
                {
                    NpcId = Data?.Id ?? string.Empty,
                    OldValue = previous,
                    NewValue = Affection,
                    MilestoneId = string.Empty
                });
        }

        private void ResolveRuntimeReferences()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }

            if (_dialogue == null)
            {
                ServiceLocator.TryGet(out _dialogue);
            }

            if (_quests == null)
            {
                ServiceLocator.TryGet(out _quests);
            }

            if (_shop == null)
            {
                ServiceLocator.TryGet(out _shop);
            }
        }

        private bool CanInteractWithConfiguredRole()
        {
            if (Data == null)
            {
                return false;
            }

            return Data.IsVendor
                ? _shop != null && !_shop.IsOpen
                : _dialogue != null && !_dialogue.IsOpen;
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void BuildPrompt()
        {
            var promptObject = new GameObject("NpcInteractionPrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            _prompt = promptObject.AddComponent<TextMesh>();
            _prompt.anchor = TextAnchor.LowerCenter;
            _prompt.alignment = TextAlignment.Center;
            _prompt.fontSize = 48;
            _prompt.characterSize = 0.045f;
            _prompt.color = new Color(0.93f, 0.86f, 0.64f, 1f);
        }

        private void RefreshPromptText()
        {
            if (_prompt == null)
            {
                return;
            }

            string displayName = Data?.DisplayName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = "药老";
            }

            _prompt.text = string.Format(
                Data != null && Data.IsVendor
                    ? VendorInteractionPromptDefaultValue
                    : InteractionPromptDefaultValue,
                displayName);
        }

        private void SetPromptVisible(bool visible)
        {
            if (_prompt != null && _prompt.gameObject.activeSelf != visible)
            {
                _prompt.gameObject.SetActive(visible);
            }
        }

        private void FacePromptToCamera()
        {
            Camera camera = Camera.main;
            if (_prompt == null || camera == null)
            {
                return;
            }

            Vector3 forward = _prompt.transform.position
                - camera.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                _prompt.transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }
}
