namespace Wendao.Systems.Faction
{
    public static class FactionContentIds
    {
        public const string Danding = "faction_danding";
        public const string JoinedToastKey = "ui_faction_danding_joined";
        public const string JoinedToastDefault = "已记名丹鼎宗，可积累宗门声望。";
        public const string RankUpToastKey = "ui_faction_rank_up";
        public const string RankUpToastDefault = "丹鼎宗声望提升至第 {0} 阶。";

        public static readonly int[] RankThresholds =
        {
            0,
            100,
            300,
            600,
            1000,
            2000
        };
    }
}
