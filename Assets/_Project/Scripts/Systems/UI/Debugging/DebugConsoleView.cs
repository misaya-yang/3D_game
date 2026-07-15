#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Systems.Debugging;
using Wendao.Systems.Input;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Debugging
{
    public sealed class DebugConsoleView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_debug_console_title";
        public const string TitleDefaultValue =
            "调试控制台（` 开关 · Enter 执行 · Esc 关闭）";
        public const string PlaceholderLocalizationKey =
            "ui_debug_console_placeholder";
        public const string PlaceholderDefaultValue = "输入命令，例如 /help";
        public const string ReadyLocalizationKey = "ui_debug_console_ready";
        public const string ReadyDefaultValue = "输入 /help 查看可用命令。";
        public const string ClearLocalizationKey = "ui_debug_console_clear";
        public const string ClearDefaultValue = "控制台记录已清空。";

        private const int MaximumHistoryLines = 18;

        private readonly List<string> _history = new List<string>();
        private CanvasGroup _canvasGroup;
        private InputField _inputField;
        private Text _output;
        private IDebugConsoleService _console;
        private IPlayerInputSource _playerInput;
        private bool _restorePlayerInput;

        public bool IsOpen { get; private set; }
        public string LastCommand { get; private set; } = string.Empty;
        public DebugCommandResult LastResult { get; private set; }
        public string HistoryText => _output != null ? _output.text : string.Empty;

        private void Awake()
        {
            BuildView();
            AppendLine(ReadyDefaultValue);
            ApplyOpenState(false);
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                SetOpen(!IsOpen);
                return;
            }

            if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
            }
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
            {
                return;
            }

            if (open)
            {
                ServiceLocator.TryGet(out _playerInput);
                _restorePlayerInput = _playerInput != null
                    && _playerInput.IsEnabled;
                _playerInput?.SetEnabled(false);
            }

            ApplyOpenState(open);
            if (open)
            {
                _inputField.text = string.Empty;
                EventSystem.current?.SetSelectedGameObject(_inputField.gameObject);
                _inputField.ActivateInputField();
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
                if (_restorePlayerInput)
                {
                    _playerInput?.SetEnabled(true);
                }

                _restorePlayerInput = false;
                _playerInput = null;
            }
        }

        public DebugCommandResult ExecuteCommand(string commandLine)
        {
            string command = commandLine?.Trim() ?? string.Empty;
            LastCommand = command;
            if (string.Equals(
                    command,
                    "/clear",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                _history.Clear();
                LastResult = DebugCommandResult.Success(
                    ClearLocalizationKey,
                    ClearDefaultValue);
                AppendResult(command, LastResult);
                return LastResult;
            }

            if (_console == null)
            {
                ServiceLocator.TryGet(out _console);
            }

            LastResult = _console != null
                ? _console.Execute(command)
                : DebugCommandResult.Failure(
                    DebugConsoleService.ServiceUnavailableLocalizationKey,
                    "调试命令服务尚未就绪。");
            AppendResult(command, LastResult);
            return LastResult;
        }

        private void HandleEndEdit(string commandLine)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(commandLine))
            {
                return;
            }

            ExecuteCommand(commandLine);
            _inputField.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(_inputField.gameObject);
            _inputField.ActivateInputField();
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "DebugConsoleCanvas",
                1000);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image panel = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "DebugConsolePanel",
                new Color(0.025f, 0.035f, 0.03f, 0.97f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(1120f, 500f),
                new Vector2(0f, -270f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "DebugConsoleTitle",
                TitleDefaultValue,
                24,
                new Color(0.72f, 0.92f, 0.74f, 1f),
                new Vector2(1060f, 48f),
                new Vector2(0f, 218f));

            _output = RuntimeUiFactory.CreateText(
                panel.transform,
                "DebugConsoleOutput",
                string.Empty,
                19,
                new Color(0.86f, 0.9f, 0.82f, 1f),
                new Vector2(1040f, 330f),
                new Vector2(0f, 22f));
            _output.alignment = TextAnchor.UpperLeft;
            _output.horizontalOverflow = HorizontalWrapMode.Wrap;
            _output.verticalOverflow = VerticalWrapMode.Truncate;

            Image inputBackground = RuntimeUiFactory.CreateImage(
                panel.transform,
                "DebugConsoleInputBackground",
                new Color(0.08f, 0.12f, 0.095f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1040f, 58f),
                new Vector2(0f, -208f));
            Text inputText = RuntimeUiFactory.CreateText(
                inputBackground.transform,
                "DebugConsoleInputText",
                string.Empty,
                21,
                new Color(0.96f, 0.96f, 0.9f, 1f),
                new Vector2(1000f, 48f),
                Vector2.zero);
            inputText.alignment = TextAnchor.MiddleLeft;
            Text placeholder = RuntimeUiFactory.CreateText(
                inputBackground.transform,
                "DebugConsolePlaceholder",
                PlaceholderDefaultValue,
                21,
                new Color(0.58f, 0.64f, 0.58f, 1f),
                new Vector2(1000f, 48f),
                Vector2.zero);
            placeholder.alignment = TextAnchor.MiddleLeft;

            _inputField = inputBackground.gameObject.AddComponent<InputField>();
            _inputField.targetGraphic = inputBackground;
            _inputField.textComponent = inputText;
            _inputField.placeholder = placeholder;
            _inputField.lineType = InputField.LineType.SingleLine;
            _inputField.characterLimit = 256;
            _inputField.onEndEdit.AddListener(HandleEndEdit);
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }

        private void AppendResult(string command, DebugCommandResult result)
        {
            if (!string.IsNullOrEmpty(command))
            {
                AppendLine("> " + command);
            }

            AppendLine((result.Succeeded ? "✓ " : "× ") + result.DefaultValue);
        }

        private void AppendLine(string value)
        {
            _history.Add(value ?? string.Empty);
            while (_history.Count > MaximumHistoryLines)
            {
                _history.RemoveAt(0);
            }

            if (_output != null)
            {
                _output.text = string.Join("\n", _history);
            }
        }
    }
}
#endif
