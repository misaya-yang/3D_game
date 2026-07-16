using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wendao.UI.SceneFlow
{
    internal enum RuntimeUiTextRole
    {
        Title,
        Heading,
        Body,
        Muted,
        Accent,
        Positive,
        Warning,
        Danger
    }

    /// <summary>
    /// Runtime UI design tokens and curated CC0 sprite access for G09-01.
    /// Views keep owning their data and behavior; this class only centralizes
    /// visual language, control states and reusable surface assets.
    /// </summary>
    internal static class RuntimeUiTheme
    {
        public static readonly Color Ink = new Color(0.025f, 0.045f, 0.039f, 1f);
        public static readonly Color InkSoft = new Color(0.04f, 0.075f, 0.062f, 0.96f);
        public static readonly Color Overlay = new Color(0.008f, 0.018f, 0.016f, 0.72f);
        // Kenney frame pixels already carry a medium-brown value. These are
        // intentionally light tints: Image.color multiplies the source texture,
        // so very dark tints erase its border and make controls look disabled.
        public static readonly Color Surface = new Color(0.34f, 0.48f, 0.38f, 0.98f);
        public static readonly Color SurfaceRaised = new Color(0.50f, 0.65f, 0.52f, 0.98f);
        public static readonly Color SurfaceInset = new Color(0.24f, 0.34f, 0.28f, 0.94f);
        public static readonly Color Jade = new Color(0.31f, 0.68f, 0.51f, 1f);
        public static readonly Color JadeBright = new Color(0.49f, 0.82f, 0.61f, 1f);
        public static readonly Color Gold = new Color(0.91f, 0.76f, 0.39f, 1f);
        public static readonly Color GoldSoft = new Color(0.92f, 0.84f, 0.62f, 1f);
        public static readonly Color Parchment = new Color(0.94f, 0.92f, 0.82f, 1f);
        public static readonly Color Muted = new Color(0.69f, 0.75f, 0.68f, 1f);
        public static readonly Color Positive = new Color(0.50f, 0.82f, 0.58f, 1f);
        public static readonly Color Warning = new Color(0.95f, 0.68f, 0.31f, 1f);
        public static readonly Color Danger = new Color(0.86f, 0.31f, 0.25f, 1f);
        public static readonly Color Mana = new Color(0.25f, 0.62f, 0.82f, 1f);

        private const string FrameRoot = "UI/G09/Frames/";
        private const string IconRoot = "UI/G09/Icons/";
        private const string BackgroundRoot = "UI/G09/Backgrounds/";

        private static readonly Dictionary<string, Sprite> Sprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static Sprite PanelSprite => LoadSprite(
            FrameRoot + "panel_brown",
            new Vector4(12f, 12f, 12f, 12f));

        public static Sprite InsetSprite => LoadSprite(
            FrameRoot + "panelInset_brown",
            new Vector4(10f, 10f, 10f, 10f));

        public static Sprite ButtonSprite => LoadSprite(
            FrameRoot + "buttonLong_brown",
            new Vector4(10f, 10f, 10f, 10f));

        public static Sprite SquareButtonSprite => LoadSprite(
            FrameRoot + "buttonSquare_brown",
            new Vector4(9f, 9f, 9f, 9f));

        public static Sprite MainMenuBackground => LoadSprite(
            BackgroundRoot + "main_menu_misty_path_v1",
            Vector4.zero);

        public static Sprite GetIcon(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? null
                : LoadSprite(IconRoot + name, Vector4.zero);
        }

        public static void StylePanel(Image image, bool inset = false)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = inset ? InsetSprite : PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = inset ? SurfaceInset : Surface;
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image
                ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = ButtonSprite;
                image.type = Image.Type.Sliced;
                image.color = primary ? Jade : SurfaceRaised;
                button.targetGraphic = image;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = primary ? Jade : SurfaceRaised;
            colors.highlightedColor = primary ? JadeBright : new Color(
                0.24f,
                0.39f,
                0.30f,
                1f);
            colors.selectedColor = Gold;
            colors.pressedColor = new Color(0.10f, 0.18f, 0.14f, 1f);
            colors.disabledColor = new Color(0.08f, 0.10f, 0.09f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
        }

        public static void StyleText(Text text, RuntimeUiTextRole role)
        {
            if (text == null)
            {
                return;
            }

            switch (role)
            {
                case RuntimeUiTextRole.Title:
                    text.color = Gold;
                    text.fontStyle = FontStyle.Bold;
                    break;
                case RuntimeUiTextRole.Heading:
                    text.color = GoldSoft;
                    text.fontStyle = FontStyle.Bold;
                    break;
                case RuntimeUiTextRole.Muted:
                    text.color = Muted;
                    break;
                case RuntimeUiTextRole.Accent:
                    text.color = JadeBright;
                    text.fontStyle = FontStyle.Bold;
                    break;
                case RuntimeUiTextRole.Positive:
                    text.color = Positive;
                    break;
                case RuntimeUiTextRole.Warning:
                    text.color = Warning;
                    break;
                case RuntimeUiTextRole.Danger:
                    text.color = Danger;
                    break;
                default:
                    text.color = Parchment;
                    break;
            }
        }

        public static void Focus(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        public static void FocusFirstSelectable(GameObject scope)
        {
            if (scope == null || EventSystem.current == null)
            {
                return;
            }

            Selectable[] selectables = scope.GetComponentsInChildren<Selectable>(true);
            Selectable first = null;
            for (int index = 0; index < selectables.Length; index++)
            {
                Selectable candidate = selectables[index];
                if (candidate is Button button)
                {
                    StyleButton(button, IsPrimaryControl(button.name));
                }
                if (candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.IsInteractable()
                    && first == null)
                {
                    first = candidate;
                }
            }

            Focus(first);
        }

        public static void ApplyNamedImageStyle(Image image, string objectName)
        {
            if (image == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            if (objectName.EndsWith("Panel", StringComparison.Ordinal)
                || objectName.Contains("PromptPanel")
                || objectName.Contains("Popup"))
            {
                image.sprite = PanelSprite;
                image.type = Image.Type.Sliced;
                image.color = Surface;
                return;
            }

            if (objectName.Contains("Button")
                || objectName.StartsWith("QuestRow", StringComparison.Ordinal)
                || objectName.StartsWith("Recipe", StringComparison.Ordinal))
            {
                image.sprite = ButtonSprite;
                image.type = Image.Type.Sliced;
                image.color = SurfaceRaised;
                return;
            }

            if (objectName.Contains("Slot")
                && !objectName.Contains("Label")
                && !objectName.Contains("Cooldown"))
            {
                image.sprite = SquareButtonSprite;
                image.type = Image.Type.Sliced;
                image.color = SurfaceInset;
            }
        }

        private static Sprite LoadSprite(string resourcePath, Vector4 border)
        {
            string cacheKey = resourcePath + "|" + border;
            if (Sprites.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);
            sprite.name = texture.name + "_RuntimeSprite";
            Sprites[cacheKey] = sprite;
            return sprite;
        }

        private static bool IsPrimaryControl(string objectName)
        {
            return !string.IsNullOrEmpty(objectName)
                && (objectName.Contains("Start")
                    || objectName.Contains("Confirm")
                    || objectName.Contains("Craft")
                    || objectName.Contains("Travel")
                    || objectName.Contains("Claim")
                    || objectName.Contains("Revive")
                    || objectName.Contains("Breakthrough"));
        }
    }
}
