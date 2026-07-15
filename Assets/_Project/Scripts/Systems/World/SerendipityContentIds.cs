namespace Wendao.Systems.World
{
    public static class SerendipityContentIds
    {
        public const string QingshiHerbSpirit =
            "serendipity_qingshi_herb_spirit";
        public const string CangwuMistStele =
            "serendipity_cangwu_mist_stele";
        public const string CangwuCliffBox =
            "serendipity_cangwu_cliff_box";
        public const string BlackwindEchoCache =
            "serendipity_blackwind_echo_cache";

        public static readonly string[] All =
        {
            QingshiHerbSpirit,
            CangwuMistStele,
            CangwuCliffBox,
            BlackwindEchoCache
        };
    }

    public static class SerendipityEvents
    {
        public const string Triggered = "OnSerendipityTriggered";
    }
}
