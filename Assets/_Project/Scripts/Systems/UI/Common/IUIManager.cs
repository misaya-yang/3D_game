using System;

namespace Wendao.UI.Common
{
    public static class UiPanelIds
    {
        public const string Inventory = "panel_inventory";
        public const string Character = "panel_character";
        public const string Skill = "panel_skill";
        public const string Quest = "panel_quest";
        public const string Map = "panel_map";
        public const string Alchemy = "panel_alchemy";
        public const string Shop = "panel_shop";
        public const string Pause = "panel_pause";
    }

    public interface IUIManager
    {
        bool HasOpenPanel { get; }

        void ShowPanel(string panelId);
        void HidePanel(string panelId);
        void HideAllPanels();
        bool IsPanelOpen(string panelId);
        void TogglePanel(string panelId);
        bool CloseTopPanel();
        void HandleCancel();
        void ShowToast(string message, float duration = 2f);
        void ShowConfirm(string message, Action onYes, Action onNo = null);
        void SetHudVisible(bool visible);
    }
}
