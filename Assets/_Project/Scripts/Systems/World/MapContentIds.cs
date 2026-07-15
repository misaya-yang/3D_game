namespace Wendao.Systems.World
{
    public static class MapContentIds
    {
        public const string QingshiMap = "map_qingshi";
        public const string CangwuMap = "map_cangwu";
        public const string BlackwindMap = "map_blackwind";

        public const string QingshiTownTeleport = "teleport_qingshi_town";
        public const string CangwuGateTeleport = "teleport_cangwu_gate";

        public const string BlackwindEntrance = "entrance_blackwind_cangwu";
        public const string BlackwindHealingSpring =
            "spring_blackwind_b4_heal";
        public const string BlackwindBranchChest =
            "chest_blackwind_b3_branch";
        public const string BlackwindEntryChest =
            "chest_blackwind_b1_supply";
        public const string BlackwindDeepChest =
            "chest_blackwind_b4_cache";
        public const string BlackwindSpikeHazard =
            "hazard_blackwind_b3_spikes";

        public static string GetBlackwindSpawnId(int floor)
        {
            return $"spawn_blackwind_b{UnityEngine.Mathf.Clamp(floor, 1, 5)}";
        }

        public const string CangwuPathOpenFlag =
            "quest_flag_main_cangwu_path_open";

        public const string CangwuMistChest = "chest_cangwu_mist_01";
        public const string CangwuCaveChest = "chest_cangwu_cave_01";
        public const string CangwuTerraceChest = "chest_cangwu_terrace_01";
    }
}
