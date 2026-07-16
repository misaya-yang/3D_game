using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Wendao.UI.SceneFlow
{
    internal static class RuntimeUiFactory
    {
        private static readonly string[] PreferredFontNames =
        {
            "PingFang SC",
            "Hiragino Sans GB",
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "Source Han Sans SC",
            "Arial Unicode MS"
        };

        private static Font _defaultFont;

        public static Canvas CreateCanvas(
            Transform parent,
            string name,
            int sortingOrder = 10)
        {
            var canvasObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            // A component added to an already-active object receives OnEnable
            // before AssignDefaultActions. Explicitly enabling the assigned
            // asset prevents a visually responsive EventSystem whose submit
            // and click actions never actually fire in a built Player.
            inputModule.actionsAsset?.Enable();
        }

        public static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            RuntimeUiTheme.ApplyNamedImageStyle(image, name);
            return image;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Text text = textObject.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.08f;
            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        public static Image CreatePanel(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            bool inset = false)
        {
            Image image = CreateImage(
                parent,
                name,
                inset ? RuntimeUiTheme.SurfaceInset : RuntimeUiTheme.Surface,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            RuntimeUiTheme.StylePanel(image, inset);
            return image;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 position,
            bool primary = false,
            string iconName = null)
        {
            Image image = CreateImage(
                parent,
                name,
                primary ? RuntimeUiTheme.Jade : RuntimeUiTheme.SurfaceRaised,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            RuntimeUiTheme.StyleButton(button, primary);

            float iconSpace = string.IsNullOrWhiteSpace(iconName) ? 0f : 46f;
            Text text = CreateText(
                image.transform,
                "Label",
                label,
                Mathf.RoundToInt(Mathf.Clamp(size.y * 0.34f, 21f, 30f)),
                RuntimeUiTheme.Parchment,
                new Vector2(size.x - 28f - iconSpace, size.y - 12f),
                new Vector2(iconSpace * 0.35f, 0f));
            RuntimeUiTheme.StyleText(text, RuntimeUiTextRole.Body);

            if (!string.IsNullOrWhiteSpace(iconName))
            {
                Image icon = CreateImage(
                    image.transform,
                    "Icon",
                    RuntimeUiTheme.GoldSoft,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(34f, 34f),
                    new Vector2(-size.x * 0.34f, 0f));
                icon.sprite = RuntimeUiTheme.GetIcon(iconName);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            return button;
        }

        public static Image CreateIcon(
            Transform parent,
            string name,
            string iconName,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            Image icon = CreateImage(
                parent,
                name,
                color,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            icon.sprite = RuntimeUiTheme.GetIcon(iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        public static Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position)
        {
            Image background = CreateImage(
                parent,
                name,
                RuntimeUiTheme.SurfaceInset,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            background.sprite = RuntimeUiTheme.InsetSprite;
            background.type = Image.Type.Sliced;

            Image fill = CreateImage(
                background.transform,
                "Fill",
                RuntimeUiTheme.Jade,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-22f, 18f),
                Vector2.zero);
            fill.sprite = RuntimeUiTheme.ButtonSprite;
            fill.type = Image.Type.Sliced;
            RectTransform fillRect = fill.rectTransform;
            fillRect.offsetMin = new Vector2(11f, -9f);
            fillRect.offsetMax = new Vector2(-11f, 9f);

            Image handle = CreateImage(
                background.transform,
                "Handle",
                RuntimeUiTheme.Gold,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(40f, 40f),
                Vector2.zero);
            handle.sprite = RuntimeUiTheme.SquareButtonSprite;
            handle.type = Image.Type.Sliced;

            var slider = background.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;

            ColorBlock colors = slider.colors;
            colors.normalColor = RuntimeUiTheme.Gold;
            colors.highlightedColor = RuntimeUiTheme.GoldSoft;
            colors.selectedColor = RuntimeUiTheme.JadeBright;
            colors.pressedColor = RuntimeUiTheme.Jade;
            colors.disabledColor = RuntimeUiTheme.Muted;
            colors.fadeDuration = 0.08f;
            slider.colors = colors;
            return slider;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font GetDefaultFont()
        {
            if (_defaultFont == null)
            {
                string[] installedFontNames = Font.GetOSInstalledFontNames();
                for (int preferredIndex = 0;
                     preferredIndex < PreferredFontNames.Length && _defaultFont == null;
                     preferredIndex++)
                {
                    for (int installedIndex = 0;
                         installedIndex < installedFontNames.Length;
                         installedIndex++)
                    {
                        if (!string.Equals(
                                PreferredFontNames[preferredIndex],
                                installedFontNames[installedIndex],
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        _defaultFont = Font.CreateDynamicFontFromOSFont(
                            installedFontNames[installedIndex],
                            32);
                        break;
                    }
                }

                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }

            return _defaultFont;
        }
    }
}
