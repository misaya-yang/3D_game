namespace Wendao.Systems.Quest
{
    public static class QuestContentIds
    {
        public const string MainRootAwakening = "quest_main_01_01";
        public const string MainHuntWolves = "quest_main_01_02";
        public const string MainGatherQingxin = "quest_main_01_03";
        public const string MainCraftManaPotion = "quest_main_01_04";
        public const string MainOpenCangwuPath = "quest_main_01_05";
        public const string MainCangwuTrial = "quest_main_01_06";
        public const string MainFoundationClue = "quest_main_01_07";
        public const string MainFoundationBreakthrough = "quest_main_01_08";
        public const string MainGoldenCoreBreakthrough = "quest_main_01_09";
        public const string MainDefeatStoneGeneral = "quest_main_01_10";

        public static readonly string[] MainChapterOne =
        {
            MainRootAwakening,
            MainHuntWolves,
            MainGatherQingxin,
            MainCraftManaPotion,
            MainOpenCangwuPath,
            MainCangwuTrial,
            MainFoundationClue,
            MainFoundationBreakthrough,
            MainGoldenCoreBreakthrough,
            MainDefeatStoneGeneral
        };

        public const string SideHerb = "quest_side_herb_01";
        public const string SideBandit = "quest_side_bandit_01";
        public const string SideHermit = "quest_side_hermit_01";
        public static readonly string[] SideQuests =
        {
            SideHerb,
            SideBandit,
            SideHermit
        };

        public const string DailyHunt = "quest_daily_hunt";
        public const string DailyGather = "quest_daily_gather";
        public static readonly string[] DailyQuests =
        {
            DailyHunt,
            DailyGather
        };

        public const string DandingGuideNpc = "npc_danding_guide";
        public const string TrainerNpc = "npc_trainer";
        public const string HermitNpc = "npc_hermit";
        public const string BanditEnemy = "enemy_bandit";
        public const string HermitLetterItem = "item_quest_hermit_letter";
        public const string DandingFaction = "faction_danding";

        public const string GreyWolfEnemy = "enemy_wolf_gray";
        public const string EliteWolfEnemy = "enemy_wolf_elite";
        public const string StoneGeneralEnemy = "enemy_boss_stone_general";
        public const string YaoLaoNpc = "npc_yaolao";
        public const string CangwuGuardNpc = "npc_cangwu_guard";
        public const string BlackwindEchoNpc = "npc_blackwind_echo";
        public const string QingshiSecretPath = "area_qingshi_secret_path";
        public const string BlackwindEntrance = "blackwind_entrance";
        public const string HuntStartDialogue = "dlg_main_01_02_start";
        public const string HuntCompleteDialogue = "dlg_main_01_02_complete";

        public static string GetStartDialogueId(int chapterStep)
        {
            return $"dlg_main_01_{UnityEngine.Mathf.Clamp(chapterStep, 1, 10):00}_start";
        }

        public static string GetCompleteDialogueId(int chapterStep)
        {
            return $"dlg_main_01_{UnityEngine.Mathf.Clamp(chapterStep, 1, 10):00}_complete";
        }
    }
}
