using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.UI.Common;
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
        public const string PreviewLocalizationKey = "ui_root_preview_hint";
        public const string PreviewDefaultValue =
            "先行感应，可在确认前反复比较。";

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
        private bool _selectionWasRandomized;
        private int _randomPreviewSeed;

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
            _selectionWasRandomized = false;
            _randomPreviewSeed = 0;
            _selectionText.text = PreviewDefaultValue;
            _introText.text = FiveIntroDefaultValue;
            SetChoiceInteractable(true);
            _confirmButton.gameObject.SetActive(false);
            ApplyVisible(true);
            SuspendGameplayInput();
            RuntimeUiTheme.Focus(_rootButtons[0]);
        }

        public bool SelectRoot(SpiritRootType type)
        {
            ResolveServices();
            if (!IsOpen
                || _spiritRoot == null
                || _spiritRoot.HasChosenRoot
                || !IsPreviewableRoot(type))
            {
                return false;
            }

            ShowSelectedRoot(type);
            _selectionWasRandomized = false;
            return true;
        }

        public bool SelectRandom()
        {
            return SelectRandom(UnityEngine.Random.Range(0, int.MaxValue));
        }

        public bool SelectRandom(int seed)
        {
            ResolveServices();
            if (!IsOpen || _spiritRoot == null || _spiritRoot.HasChosenRoot)
            {
                return false;
            }

            var random = new System.Random(seed);
            SpiritRootType rolled = RollPreviewRoot((float)random.NextDouble());
            if (rolled == SpiritRootType.None)
            {
                return false;
            }

            ShowSelectedRoot(rolled);
            _selectionWasRandomized = true;
            _randomPreviewSeed = seed;
            return true;
        }

        public void ConfirmSelection()
        {
            ResolveServices();
            if (!IsOpen
                || SelectedRoot == SpiritRootType.None
                || _spiritRoot == null)
            {
                return;
            }

            bool committed = _selectionWasRandomized
                ? _spiritRoot.TryRandomizeRoot(_randomPreviewSeed)
                : _spiritRoot.TryChooseRoot(SelectedRoot);
            if (!committed || _spiritRoot.Root != SelectedRoot)
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
            RefreshRootButtonSelection();
            _confirmButton.gameObject.SetActive(true);
            RuntimeUiTheme.Focus(_confirmButton);
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
                RuntimeUiTheme.Overlay,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreatePanel(
                overlay.transform,
                "SpiritRootPanel",
                new Vector2(1120f, 720f),
                Vector2.zero);

            Text title = RuntimeUiFactory.CreateText(
                panel.transform,
                "SpiritRootTitle",
                TitleDefaultValue,
                48,
                RuntimeUiTheme.Gold,
                new Vector2(800f, 70f),
                new Vector2(0f, 292f));
            RuntimeUiTheme.StyleText(title, RuntimeUiTextRole.Title);
            Text prompt = RuntimeUiFactory.CreateText(
                panel.transform,
                "SpiritRootPrompt",
                PromptDefaultValue,
                23,
                RuntimeUiTheme.Muted,
                new Vector2(880f, 54f),
                new Vector2(0f, 228f));
            RuntimeUiTheme.StyleText(prompt, RuntimeUiTextRole.Muted);

            for (int index = 0; index < PickableRoots.Length; index++)
            {
                SpiritRootType root = PickableRoots[index];
                Button button = CreateButton(
                    panel.transform,
                    "RootButton_" + root,
                    GetRootNameDefaultValue(root),
                    new Vector2(184f, 82f),
                    new Vector2(-392f + index * 196f, 122f));
                button.onClick.AddListener(() => SelectRoot(root));
                _rootButtons[index] = button;
            }

            _randomButton = CreateButton(
                panel.transform,
                "RootRandomButton",
                RandomDefaultValue,
                new Vector2(280f, 76f),
                new Vector2(0f, 20f),
                false,
                "star");
            _randomButton.onClick.AddListener(() => SelectRandom());

            _selectionText = RuntimeUiFactory.CreateText(
                panel.transform,
                "RootSelectionResult",
                PreviewDefaultValue,
                28,
                RuntimeUiTheme.GoldSoft,
                new Vector2(880f, 54f),
                new Vector2(0f, -82f));
            RuntimeUiTheme.StyleText(_selectionText, RuntimeUiTextRole.Heading);
            _introText = RuntimeUiFactory.CreateText(
                panel.transform,
                "RootIntro",
                FiveIntroDefaultValue,
                23,
                RuntimeUiTheme.Parchment,
                new Vector2(840f, 100f),
                new Vector2(0f, -164f));
            RuntimeUiTheme.StyleText(_introText, RuntimeUiTextRole.Body);

            _confirmButton = CreateButton(
                panel.transform,
                "RootConfirmButton",
                ConfirmDefaultValue,
                new Vector2(310f, 88f),
                new Vector2(0f, -278f),
                true,
                "checkmark");
            _confirmButton.onClick.AddListener(ConfirmSelection);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 position,
            bool primary = false,
            string iconName = null)
        {
            return RuntimeUiFactory.CreateButton(
                parent,
                name,
                label,
                size,
                position,
                primary,
                iconName);
        }

        private void RefreshRootButtonSelection()
        {
            for (int index = 0; index < _rootButtons.Length; index++)
            {
                Button button = _rootButtons[index];
                bool selected = PickableRoots[index] == SelectedRoot;
                RuntimeUiTheme.StyleButton(button, selected);
            }

            RuntimeUiTheme.StyleButton(
                _randomButton,
                SelectedRoot == SpiritRootType.Heaven
                    || SelectedRoot == SpiritRootType.Waste);
        }

        private static bool IsPreviewableRoot(SpiritRootType type)
        {
            for (int index = 0; index < PickableRoots.Length; index++)
            {
                if (PickableRoots[index] == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static SpiritRootType RollPreviewRoot(float normalizedRoll)
        {
            SpiritRootConfig config = ConfigDatabase.Instance?.SpiritRoot;
            if (config?.Roots == null || config.Roots.Length == 0)
            {
                return SpiritRootType.None;
            }

            float totalWeight = 0f;
            for (int index = 0; index < config.Roots.Length; index++)
            {
                SpiritRootEntry entry = config.Roots[index];
                if (entry != null && entry.Type != SpiritRootType.None)
                {
                    totalWeight += Mathf.Max(0f, entry.Weight);
                }
            }

            if (totalWeight <= 0f)
            {
                return SpiritRootType.None;
            }

            float target = Mathf.Clamp01(normalizedRoll) * totalWeight;
            SpiritRootType fallback = SpiritRootType.None;
            for (int index = 0; index < config.Roots.Length; index++)
            {
                SpiritRootEntry entry = config.Roots[index];
                if (entry == null
                    || entry.Type == SpiritRootType.None
                    || entry.Weight <= 0f)
                {
                    continue;
                }

                fallback = entry.Type;
                target -= entry.Weight;
                if (target <= 0f)
                {
                    return entry.Type;
                }
            }

            return fallback;
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
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager uiManager))
            {
                uiManager.SetHudVisible(!visible);
            }
        }
    }
}
