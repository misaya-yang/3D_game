using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Cultivation
{
    public sealed class SpiritRootSelectionView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_root_selection_title";
        public const string TitleDefaultValue = "择定灵根";
        public const string PromptLocalizationKey = "ui_root_selection_prompt";
        public const string PromptDefaultValue = "灵根一经选定，便不可更改。";
        public const string RandomLocalizationKey = "ui_root_random";
        public const string RandomDefaultValue = "随机感应";
        public const string SelectedLocalizationKey = "ui_root_selected";
        public const string SelectedDefaultValue = "已择：{0}";
        public const string ConfirmLocalizationKey = "ui_root_confirm";
        public const string ConfirmDefaultValue = "踏上仙途";

        public const string FiveIntroLocalizationKey = "root_intro_five";
        public const string FiveIntroDefaultValue =
            "五行之力，各有所长。择一深耕，可引元素相生相克。";
        public const string WasteIntroLocalizationKey = "root_intro_waste";
        public const string WasteIntroDefaultValue =
            "废脉难修法，却可锤炼肉身。路途漫长，厚积薄发。";
        public const string HeavenIntroLocalizationKey = "root_intro_heaven";
        public const string HeavenIntroDefaultValue =
            "天灵罕见，气海充盈。修炼速度显著提升，万事慎傲。";

        private static readonly SpiritRootType[] PickableRoots =
        {
            SpiritRootType.Metal,
            SpiritRootType.Wood,
            SpiritRootType.Water,
            SpiritRootType.Fire,
            SpiritRootType.Earth
        };

        private readonly Button[] _rootButtons = new Button[PickableRoots.Length];

        private CanvasGroup _canvasGroup;
        private Button _randomButton;
        private Button _confirmButton;
        private Text _selectionText;
        private Text _introText;
        private ISpiritRootService _spiritRoot;
        private IPlayerInputSource _input;
        private bool _initialized;
        private bool _inputSuspended;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public SpiritRootType SelectedRoot { get; private set; }
        public string SelectedIntroLocalizationKey { get; private set; } = string.Empty;
        public string SelectedIntroDefaultValue { get; private set; } = string.Empty;
        public int PickableButtonCount => _rootButtons.Length;

        private void Awake()
        {
            BuildView();
            ApplyVisible(false);
        }

        private void Update()
        {
            if (!_initialized)
            {
                if (!ServiceLocator.TryGet(out _spiritRoot))
                {
                    return;
                }

                _initialized = true;
                if (_spiritRoot.HasChosenRoot)
                {
                    ApplyVisible(false);
                }
                else
                {
                    OpenSelection();
                }
            }

            if (IsOpen && !_inputSuspended)
            {
                SuspendGameplayInput();
            }
        }

        private void OnDisable()
        {
            RestoreGameplayInput();
        }

        public void OpenSelection()
        {
            ResolveServices();
            if (_spiritRoot == null || _spiritRoot.HasChosenRoot)
            {
                return;
            }

            SelectedRoot = SpiritRootType.None;
            SelectedIntroLocalizationKey = string.Empty;
            SelectedIntroDefaultValue = string.Empty;
            _selectionText.text = PromptDefaultValue;
            _introText.text = FiveIntroDefaultValue;
            SetChoiceInteractable(true);
            _confirmButton.gameObject.SetActive(false);
            ApplyVisible(true);
            SuspendGameplayInput();
        }

        public bool SelectRoot(SpiritRootType type)
        {
            ResolveServices();
            if (!IsOpen || _spiritRoot == null || !_spiritRoot.TryChooseRoot(type))
            {
                return false;
            }

            ShowSelectedRoot(type);
            return true;
        }

        public bool SelectRandom()
        {
            ResolveServices();
            if (!IsOpen || _spiritRoot == null || !_spiritRoot.TryRandomizeRoot())
            {
                return false;
            }

            ShowSelectedRoot(_spiritRoot.Root);
            return true;
        }

        public void ConfirmSelection()
        {
            if (!IsOpen || SelectedRoot == SpiritRootType.None)
            {
                return;
            }

            ApplyVisible(false);
            RestoreGameplayInput();
        }

        public static string GetIntroLocalizationKey(SpiritRootType type)
        {
            switch (type)
            {
                case SpiritRootType.Heaven:
                    return HeavenIntroLocalizationKey;
                case SpiritRootType.Waste:
                    return WasteIntroLocalizationKey;
                case SpiritRootType.Metal:
                case SpiritRootType.Wood:
                case SpiritRootType.Water:
                case SpiritRootType.Fire:
                case SpiritRootType.Earth:
                    return FiveIntroLocalizationKey;
                default:
                    return string.Empty;
            }
        }

        public static string GetIntroDefaultValue(SpiritRootType type)
        {
            switch (type)
            {
                case SpiritRootType.Heaven:
                    return HeavenIntroDefaultValue;
                case SpiritRootType.Waste:
                    return WasteIntroDefaultValue;
                case SpiritRootType.Metal:
                case SpiritRootType.Wood:
                case SpiritRootType.Water:
                case SpiritRootType.Fire:
                case SpiritRootType.Earth:
                    return FiveIntroDefaultValue;
                default:
                    return string.Empty;
            }
        }

        public static string GetRootNameLocalizationKey(SpiritRootType type)
        {
            return "root_name_" + type.ToString().ToLowerInvariant();
        }

        public static string GetRootNameDefaultValue(SpiritRootType type)
        {
            switch (type)
            {
                case SpiritRootType.Metal:
                    return "金灵根";
                case SpiritRootType.Wood:
                    return "木灵根";
                case SpiritRootType.Water:
                    return "水灵根";
                case SpiritRootType.Fire:
                    return "火灵根";
                case SpiritRootType.Earth:
                    return "土灵根";
                case SpiritRootType.Heaven:
                    return "天灵根";
                case SpiritRootType.Waste:
                    return "废脉";
                default:
                    return string.Empty;
            }
        }

        private void ShowSelectedRoot(SpiritRootType type)
        {
            SelectedRoot = type;
            SelectedIntroLocalizationKey = GetIntroLocalizationKey(type);
            SelectedIntroDefaultValue = GetIntroDefaultValue(type);
            _selectionText.text = string.Format(
                SelectedDefaultValue,
                GetRootNameDefaultValue(type));
            _introText.text = SelectedIntroDefaultValue;
            SetChoiceInteractable(false);
            _confirmButton.gameObject.SetActive(true);
            _confirmButton.Select();
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "SpiritRootSelectionCanvas",
                300);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "SpiritRootOverlay",
                new Color(0.015f, 0.025f, 0.02f, 0.88f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "SpiritRootPanel",
                new Color(0.055f, 0.09f, 0.075f, 0.98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1060f, 700f),
                Vector2.zero);

            RuntimeUiFactory.CreateText(
                panel.transform,
                "SpiritRootTitle",
                TitleDefaultValue,
                42,
                new Color(0.9f, 0.82f, 0.58f, 1f),
                new Vector2(800f, 70f),
                new Vector2(0f, 285f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "SpiritRootPrompt",
                PromptDefaultValue,
                23,
                new Color(0.82f, 0.86f, 0.77f, 1f),
                new Vector2(880f, 54f),
                new Vector2(0f, 220f));

            for (int index = 0; index < PickableRoots.Length; index++)
            {
                SpiritRootType root = PickableRoots[index];
                Button button = CreateButton(
                    panel.transform,
                    "RootButton_" + root,
                    GetRootNameDefaultValue(root),
                    new Vector2(170f, 70f),
                    new Vector2(-360f + index * 180f, 115f));
                button.onClick.AddListener(() => SelectRoot(root));
                _rootButtons[index] = button;
            }

            _randomButton = CreateButton(
                panel.transform,
                "RootRandomButton",
                RandomDefaultValue,
                new Vector2(260f, 66f),
                new Vector2(0f, 25f));
            _randomButton.onClick.AddListener(() => SelectRandom());

            _selectionText = RuntimeUiFactory.CreateText(
                panel.transform,
                "RootSelectionResult",
                PromptDefaultValue,
                28,
                new Color(0.92f, 0.84f, 0.62f, 1f),
                new Vector2(880f, 54f),
                new Vector2(0f, -70f));
            _introText = RuntimeUiFactory.CreateText(
                panel.transform,
                "RootIntro",
                FiveIntroDefaultValue,
                23,
                new Color(0.88f, 0.9f, 0.82f, 1f),
                new Vector2(840f, 100f),
                new Vector2(0f, -150f));

            _confirmButton = CreateButton(
                panel.transform,
                "RootConfirmButton",
                ConfirmDefaultValue,
                new Vector2(260f, 70f),
                new Vector2(0f, -265f));
            _confirmButton.onClick.AddListener(ConfirmSelection);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 position)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.15f, 0.31f, 0.24f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.27f, 0.48f, 0.34f, 1f);
            colors.pressedColor = new Color(0.09f, 0.19f, 0.14f, 1f);
            colors.disabledColor = new Color(0.09f, 0.12f, 0.1f, 0.7f);
            button.colors = colors;
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                23,
                new Color(0.95f, 0.92f, 0.8f, 1f),
                size - new Vector2(12f, 8f),
                Vector2.zero);
            return button;
        }

        private void ResolveServices()
        {
            ServiceLocator.TryGet(out _spiritRoot);
            ServiceLocator.TryGet(out _input);
        }

        private void SuspendGameplayInput()
        {
            if (_inputSuspended)
            {
                return;
            }

            if (_input == null && !ServiceLocator.TryGet(out _input))
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

            bool isAlive = _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
            if (isAlive && _inputWasEnabled)
            {
                _input.SetEnabled(true);
            }

            _inputSuspended = false;
            _inputWasEnabled = false;
        }

        private void SetChoiceInteractable(bool interactable)
        {
            for (int index = 0; index < _rootButtons.Length; index++)
            {
                _rootButtons[index].interactable = interactable;
            }

            _randomButton.interactable = interactable;
        }

        private void ApplyVisible(bool visible)
        {
            IsOpen = visible;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
