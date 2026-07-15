namespace Wendao.Systems.Cultivation
{
    public static class CultivationContentIds
    {
        public const string FoundationPityFlag =
            "questFlag_main_foundation_pity";
        public const string FoundationPillGrantedFlag =
            "foundation_pill_granted";

        public const string NoNextRealmBlocker = "NoNextRealm";
        public const string NotMaxSubStageBlocker = "NotMaxSubStage";
        public const string MissingItemBlocker = "MissingItem";
        public const string InCombatBlocker = "InCombat";
        public const string WrongStateBlocker = "WrongState";

        public const string NoNextRealmMessageKey = "ui_bt_block_no_next";
        public const string NotMaxSubStageMessageKey = "ui_bt_block_stage";
        public const string MissingItemMessageKey = "ui_bt_block_item";
        public const string InCombatMessageKey = "ui_bt_block_combat";
        public const string WrongStateMessageKey = "ui_bt_block_state";

        public const string SuccessMessageKey = "ui_bt_success";
        public const string FailureCeremonyMessageKey = "ui_bt_fail";
        public const string FailureMessageKey = "ui_bt_fail_detail";

        public const string NoNextRealmDefaultValue =
            "此境之后的破境之路尚未开放。";
        public const string NotMaxSubStageDefaultValue =
            "需先修至{0}第{1}层，方可破境。";
        public const string MissingItemDefaultValue =
            "缺少{0}。{1}";
        public const string InCombatDefaultValue =
            "气机未平，脱战五秒后方可破境。";
        public const string WrongStateDefaultValue =
            "此刻无法破境，请先结束当前交互。";
        public const string SuccessDefaultValue =
            "气机凝实，境界更进一步！";
        public const string FailureCeremonyDefaultValue =
            "心魔侵扰，破境未成。整理再战。";
        public const string FailureDefaultValue =
            "破境未成（本次成功率 {0:P0}）。{1} 心魔尚余 {2:0} 秒，修为距本层圆满尚差 {3:0}。";

        public const string FoundationHintKey =
            "hint_quest_main_08_yaolao";
        public const string GoldenCoreHintKey =
            "hint_quest_main_09_yaolao";
        public const string FoundationHintDefaultValue =
            "向药老请教筑基机缘。";
        public const string GoldenCoreHintDefaultValue =
            "向药老求取凝金丹，再图金丹大道。";
    }
}
