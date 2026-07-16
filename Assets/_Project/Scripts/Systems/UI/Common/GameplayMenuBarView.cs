using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Common
{
    /// <summary>
    /// Mouse-accessible shortcuts for the five core meta panels and pause.
    /// Keyboard hints remain visible inside each control, so the HUD teaches
    /// navigation without requiring the player to memorize the input map.
    /// </summary>
    public sealed class GameplayMenuBarView : MonoBehaviour
    {
        public const string InventoryLocalizationKey = "ui_nav_inventory";
        public const string InventoryDefaultValue = "I 行囊";
        public const string CharacterLocalizationKey = "ui_nav_character";
        public const string CharacterDefaultValue = "C 角色";
        public const string SkillLocalizationKey = "ui_nav_skill";
        public const string SkillDefaultValue = "K 功法";
        public const string QuestLocalizationKey = "ui_nav_quest";
        public const string QuestDefaultValue = "J 任务";
        public const string MapLocalizationKey = "ui_nav_map";
        public const string MapDefaultValue = "M 地图";
        public const string PauseLocalizationKey = "ui_nav_pause";
        public const string PauseDefaultValue = "Esc";

        private void Awake()
        {
            BuildView();
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "GameplayMenuBarCanvas",
                118);
            Image panel = RuntimeUiFactory.CreatePanel(
                canvas.transform,
                "GameplayMenuBarPanel",
                new Vector2(678f, 92f),
                Vector2.zero);
            panel.rectTransform.anchorMin = new Vector2(1f, 1f);
            panel.rectTransform.anchorMax = new Vector2(1f, 1f);
            panel.rectTransform.anchoredPosition = new Vector2(-355f, -58f);
            panel.color = new Color(0.10f, 0.16f, 0.13f, 0.88f);

            CreateNavButton(
                panel.transform,
                "InventoryNavButton",
                InventoryDefaultValue,
                "shoppingBasket",
                -275f,
                UiPanelIds.Inventory);
            CreateNavButton(
                panel.transform,
                "CharacterNavButton",
                CharacterDefaultValue,
                "trophy",
                -165f,
                UiPanelIds.Character);
            CreateNavButton(
                panel.transform,
                "SkillNavButton",
                SkillDefaultValue,
                "target",
                -55f,
                UiPanelIds.Skill);
            CreateNavButton(
                panel.transform,
                "QuestNavButton",
                QuestDefaultValue,
                "menuList",
                55f,
                UiPanelIds.Quest);
            CreateNavButton(
                panel.transform,
                "MapNavButton",
                MapDefaultValue,
                "star",
                165f,
                UiPanelIds.Map);
            CreateNavButton(
                panel.transform,
                "PauseNavButton",
                PauseDefaultValue,
                "pause",
                275f,
                UiPanelIds.Pause);
        }

        private static void CreateNavButton(
            Transform parent,
            string name,
            string label,
            string iconName,
            float x,
            string panelId)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                RuntimeUiTheme.SurfaceRaised,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(96f, 68f),
                new Vector2(x, 0f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            RuntimeUiTheme.StyleButton(button, false);
            image.sprite = RuntimeUiTheme.SquareButtonSprite;
            image.type = Image.Type.Sliced;

            RuntimeUiFactory.CreateIcon(
                image.transform,
                "Icon",
                iconName,
                new Vector2(25f, 25f),
                new Vector2(0f, 11f),
                RuntimeUiTheme.GoldSoft);
            Text text = RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                16,
                RuntimeUiTheme.Parchment,
                new Vector2(86f, 25f),
                new Vector2(0f, -19f));
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 16;
            button.onClick.AddListener(() => OpenPanel(panelId));
        }

        private static void OpenPanel(string panelId)
        {
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager manager))
            {
                manager.ShowPanel(panelId);
            }
        }
    }
}
