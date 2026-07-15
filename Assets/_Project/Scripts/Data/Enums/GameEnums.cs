namespace Wendao.Data
{
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Wind,
        Dark,
        True
    }

    public enum ElementType
    {
        None,
        Metal,
        Wood,
        Water,
        Fire,
        Earth,
        Wind,
        Lightning,
        Ice,
        Poison,
        Dark
    }

    public enum ElementReactionType
    {
        None,
        Melt,
        BurnBurst,
        Shock,
        Spread,
        Sever
    }

    public enum StatusEffectChangeType
    {
        Applied,
        Refreshed,
        StackChanged,
        Removed,
        Expired,
        Promoted
    }

    public enum CombatTeam
    {
        Neutral,
        Player,
        Enemy
    }

    public enum RealmType
    {
        Mortal = 0,
        QiCondensation = 1,
        Foundation = 2,
        GoldenCore = 3,
        NascentSoul = 4
    }

    public enum SpiritRootType
    {
        None,
        Metal,
        Wood,
        Water,
        Fire,
        Earth,
        Heaven,
        Waste
    }

    public enum EquipmentSlot
    {
        Weapon,
        Head,
        Chest,
        Legs,
        Boots,
        Accessory1,
        Accessory2,
        Treasure
    }

    public enum ItemType
    {
        Consumable,
        Material,
        Equipment,
        Quest,
        Currency,
        Recipe,
        Talisman
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum SkillType
    {
        Active,
        Passive,
        Ultimate
    }

    public enum SkillElement
    {
        None,
        Fire,
        Ice,
        Lightning,
        Poison,
        Wind,
        Metal,
        Earth
    }

    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Weekly
    }

    public enum QuestStatus
    {
        Locked,
        Available,
        Active,
        Completed,
        Failed,
        TurnedIn
    }

    public enum ObjectiveType
    {
        Kill,
        Collect,
        Talk,
        Reach,
        UseItem,
        Craft,
        Survive,
        ReachRealm
    }

    public enum EnemyRank
    {
        Normal,
        Elite,
        Boss,
        WorldBoss
    }

    public enum CraftType
    {
        Alchemy,
        Smithing,
        Talisman,
        Formation
    }

    public enum XpSourceType
    {
        Combat,
        Quest,
        Breakthrough,
        Consume,
        Other
    }

    public enum AcquireSource
    {
        Loot,
        Quest,
        Craft,
        Shop,
        Cheat,
        Gather
    }

    public enum ShopTransactionType
    {
        Buy,
        Sell
    }

    public enum WeatherId
    {
        Clear,
        Rain,
        Fog,
        Storm,
        Snow
    }

    public enum BodyLevel
    {
        Mortal = 0,
        Copper = 1,
        Diamond = 2,
        Immortal = 3,
        Eternal = 4
    }

    public enum UseEffectType
    {
        Heal,
        RestoreMana,
        AddCultivationXp,
        AddBodyXp,
        ApplyStatus,
        LearnSkill
    }

    public enum TelegraphShape
    {
        Circle,
        Line,
        Sector,
        FullScreen
    }

    public enum PlayerState
    {
        Idle,
        Move,
        Sprint,
        Jump,
        Fall,
        LightAttack,
        HeavyAttack,
        Dodge,
        Block,
        BlockHit,
        Stagger,
        SkillCast,
        Dead
    }
}
