using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.NPC;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.NPC
{
    public sealed class DialogueView : MonoBehaviour
    {
        public const string ContinueLocalizationKey = "ui_dialogue_continue";
        public const string ContinueDefaultValue = "[E] 继续";
        public const string FinishLocalizationKey = "ui_dialogue_finish";
        public const string FinishDefaultValue = "[E] 完成";
        public const string ChooseFirstLocalizationKey =
            "ui_dialogue_choose_first";
        public const string ChooseFirstDefaultValue = "[E] 选择第一项";

        private const int MaximumChoiceButtons = 3;

        private readonly Button[] _choiceButtons =
            new Button[MaximumChoiceButtons];
        private readonly Text[] _choiceLabels = new Text[MaximumChoiceButtons];

        private CanvasGroup _canvasGroup;
        private Text _speakerText;
        private Text _bodyText;
        private Text _actionHintText;
        private IDialogueService _dialogue;
        private IPlayerInputSource _input;
        private string _lastNodeId = string.Empty;
        private int _openedFrame = -1;

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;
        public string SpeakerText => _speakerText?.text ?? string.Empty;
        public string BodyText => _bodyText?.text ?? string.Empty;
        public int VisibleChoiceCount { get; private set; }
        public string CurrentTextLocalizationKey { get; private set; } =
            string.Empty;

        private void Awake()
        {
            BuildView();
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
        }

        private void Update()
        {
            ResolveServices();
            if (_dialogue == null || !_dialogue.IsOpen)
            {
                if (IsVisible)
                {
                    Refresh();
                }

                return;
            }

            string nodeId = _dialogue.CurrentNode?.NodeId ?? string.Empty;
            if (!IsVisible || nodeId != _lastNodeId)
            {
                Refresh();
            }

            if (IsInputAlive()
                && _input.InteractPressedThisFrame
                && Time.frameCount > _openedFrame)
            {
                AdvanceOrChooseFirst();
            }
        }

        public void Refresh()
        {
            ResolveServices();
            DialogueNode node = _dialogue?.CurrentNode;
            if (_dialogue == null || !_dialogue.IsOpen || node == null)
            {
                _lastNodeId = string.Empty;
                CurrentTextLocalizationKey = string.Empty;
                VisibleChoiceCount = 0;
                ApplyVisible(false);
                return;
            }

            ApplyVisible(true);
            _lastNodeId = node.NodeId ?? string.Empty;
            CurrentTextLocalizationKey = node.TextKey ?? string.Empty;
            _speakerText.text = node.SpeakerName ?? string.Empty;
            _bodyText.text = node.Text ?? string.Empty;

            VisibleChoiceCount = Mathf.Min(
                MaximumChoiceButtons,
                _dialogue.VisibleChoices.Count);
            for (int index = 0; index < MaximumChoiceButtons; index++)
            {
                bool visible = index < VisibleChoiceCount;
                _choiceButtons[index].gameObject.SetActive(visible);
                if (visible)
                {
                    _choiceLabels[index].text =
                        _dialogue.VisibleChoices[index].Text ?? string.Empty;
                }
            }

            _actionHintText.text = VisibleChoiceCount > 0
                ? ChooseFirstDefaultValue
                : node.End
                    ? FinishDefaultValue
                    : ContinueDefaultValue;
            if (VisibleChoiceCount > 0)
            {
                RuntimeUiTheme.Focus(_choiceButtons[0]);
            }
        }

        public void AdvanceOrChooseFirst()
        {
            ResolveServices();
            if (_dialogue == null || !_dialogue.IsOpen)
            {
                return;
            }

            if (_dialogue.VisibleChoices.Count > 0)
            {
                _dialogue.Choose(0);
            }
            else
            {
                _dialogue.Advance();
            }

            Refresh();
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "DialogueCanvas",
                300);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "DialogueOverlay",
                new Color(0.01f, 0.018f, 0.014f, 0.42f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "DialoguePanel",
                new Color(0.04f, 0.075f, 0.06f, 0.97f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(1380f, 350f),
                new Vector2(0f, 205f));

            _speakerText = RuntimeUiFactory.CreateText(
                panel.transform,
                "DialogueSpeaker",
                string.Empty,
                30,
                new Color(0.93f, 0.82f, 0.55f, 1f),
                new Vector2(260f, 50f),
                new Vector2(-510f, 122f));
            _bodyText = RuntimeUiFactory.CreateText(
                panel.transform,
                "DialogueBody",
                string.Empty,
                27,
                new Color(0.9f, 0.92f, 0.84f, 1f),
                new Vector2(1180f, 110f),
                new Vector2(0f, 45f));

            for (int index = 0; index < MaximumChoiceButtons; index++)
            {
                int capturedIndex = index;
                Image image = RuntimeUiFactory.CreateImage(
                    panel.transform,
                    "DialogueChoice" + index,
                    new Color(0.13f, 0.28f, 0.21f, 1f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(350f, 58f),
                    new Vector2(-380f + index * 380f, -70f));
                Button button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                RuntimeUiTheme.StyleButton(button, false);
                button.onClick.AddListener(() => Choose(capturedIndex));
                button.gameObject.SetActive(false);
                _choiceButtons[index] = button;
                _choiceLabels[index] = RuntimeUiFactory.CreateText(
                    image.transform,
                    "Label",
                    string.Empty,
                    21,
                    new Color(0.94f, 0.91f, 0.78f, 1f),
                    new Vector2(330f, 48f),
                    Vector2.zero);
            }

            _actionHintText = RuntimeUiFactory.CreateText(
                panel.transform,
                "DialogueActionHint",
                ContinueDefaultValue,
                20,
                new Color(0.65f, 0.78f, 0.68f, 1f),
                new Vector2(360f, 42f),
                new Vector2(470f, -135f));
            RuntimeUiTheme.StyleText(_speakerText, RuntimeUiTextRole.Title);
            RuntimeUiTheme.StyleText(_bodyText, RuntimeUiTextRole.Body);
            RuntimeUiTheme.StyleText(_actionHintText, RuntimeUiTextRole.Muted);
        }

        private void Choose(int choiceIndex)
        {
            ResolveServices();
            _dialogue?.Choose(choiceIndex);
            Refresh();
        }

        private void ResolveServices()
        {
            if (_dialogue == null)
            {
                ServiceLocator.TryGet(out _dialogue);
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void ApplyVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void HandleDialogueStarted(DialogueInfo info)
        {
            _openedFrame = Time.frameCount;
            Refresh();
        }

        private void HandleDialogueEnded(DialogueInfo info)
        {
            Refresh();
        }
    }
}
