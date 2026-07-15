namespace Wendao.Systems.Inventory
{
    public static class InventoryEvents
    {
        public const string ItemAcquired = "OnItemAcquired";
        public const string ItemUsed = "OnItemUsed";
        public const string CurrencyChanged = "OnCurrencyChanged";
        public const string EquipmentChanged = "OnEquipmentChanged";
        public const string EquipmentUpgraded = "OnEquipmentUpgraded";
    }

    public static class InventoryContentIds
    {
        public const string HealPotion01 = "item_potion_heal_01";
        public const string ManaPotion01 = "item_potion_mana_01";
        public const string CultivationPotion01 = "item_potion_xp_01";
        public const string BodyPotion01 = "item_potion_body_01";
        public const string FoundationPill = "item_pill_foundation";
        public const string GoldenCorePill = "item_pill_goldencore";
        public const string WoodSword = "eq_weapon_wood_sword";
        public const string IronSword = "eq_weapon_iron_sword";
        public const string WolfHair = "item_mat_wolf_hair";
        public const string QingxinGrass = "item_mat_qingxin_grass";
        public const string SpiritDust = "item_mat_spirit_dust";
        public const string BeastCore01 = "item_mat_beast_core_1";
        public const string RefineStone = "item_mat_refine_stone";
        public const string SkillScroll = "item_skill_scroll";
    }
}
