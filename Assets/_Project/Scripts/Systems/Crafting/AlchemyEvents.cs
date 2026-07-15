namespace Wendao.Systems.Crafting
{
    public static class AlchemyEvents
    {
        public const string CraftCompleted = "OnCraftCompleted";
        public const string CraftFailed = "OnCraftFailed";
    }

    public static class AlchemyContentIds
    {
        public const string HealRecipe = "recipe_heal_01";
        public const string ManaRecipe = "recipe_mana_01";
        public const string BodyRecipe = "recipe_body_01";
        public const string CultivationRecipe = "recipe_xp_01";

        public static readonly string[] Recipes =
        {
            HealRecipe,
            ManaRecipe,
            BodyRecipe,
            CultivationRecipe
        };
    }
}
