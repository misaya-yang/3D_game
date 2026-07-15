#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"

STATIC_ONLY=false
case "${1:-}" in
    "") ;;
    --static-only) STATIC_ONLY=true ;;
    *)
        printf 'Usage: %s [--static-only]\n' "$0" >&2
        exit 64
        ;;
esac

assert_file() {
    if [[ ! -f "${PROJECT_ROOT}/$1" ]]; then
        printf 'Required file missing: %s\n' "$1" >&2
        exit 1
    fi
}

assert_pattern() {
    local pattern="$1"
    local file_path="$2"
    if ! rg -q "${pattern}" "${PROJECT_ROOT}/${file_path}"; then
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_gvs_04.sh" --static-only

    local root_config="Assets/StreamingAssets/Config/SpiritRootConfig.json"
    local realm_config="Assets/StreamingAssets/Config/RealmConfig.json"
    local root_contract="Assets/_Project/Scripts/Systems/Cultivation/ISpiritRootService.cs"
    local cultivation_contract="Assets/_Project/Scripts/Systems/Cultivation/ICultivationService.cs"
    local stats_contract="Assets/_Project/Scripts/Systems/Cultivation/ICultivationStatsProvider.cs"
    local cultivation_events="Assets/_Project/Scripts/Systems/Cultivation/CultivationEvents.cs"
    local root_system="Assets/_Project/Scripts/Systems/Cultivation/SpiritRootSystem.cs"
    local cultivation="Assets/_Project/Scripts/Systems/Cultivation/CultivationManager.cs"
    local save_manager="Assets/_Project/Scripts/Data/Save/SaveManager.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local root_view="Assets/_Project/Scripts/Systems/UI/Cultivation/SpiritRootSelectionView.cs"
    local hud="Assets/_Project/Scripts/Systems/UI/Cultivation/CultivationHudView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS05PlayModeTests.cs"

    local required_files=(
        "${root_config}"
        "${realm_config}"
        "${root_contract}"
        "${cultivation_contract}"
        "${stats_contract}"
        "${cultivation_events}"
        "${root_system}"
        "${cultivation}"
        "${save_manager}"
        "${player_stats}"
        "${scene_bootstrap}"
        "${root_view}"
        "${hud}"
        "${ui_bootstrap}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    jq -e '
        .randomEnabled == true
        and .defaultPickable == ["Metal", "Wood", "Water", "Fire", "Earth"]
        and (.roots[] | select(.type == "Fire")
            | .cultivationMul == 1.1
              and .elementBonus.Fire == 0.15
              and .introDescriptionKey == "root_intro_five")
        and (.roots[] | select(.type == "Heaven")
            | .cultivationMul == 1.35
              and .weight == 0.05
              and .elementBonus.All == 0.10
              and .introDescriptionKey == "root_intro_heaven")
        and (.roots[] | select(.type == "Waste")
            | .cultivationMul == 0.55
              and .bodyMul == 1.5
              and .weight == 0.15
              and .passives.bodyPotionMul == 1.25
              and .introDescriptionKey == "root_intro_waste")
    ' "${PROJECT_ROOT}/${root_config}" >/dev/null

    jq -e '
        (.realms[] | select(.realm == 1)
            | .subStages == 9
              and .xpPerSubStage[0] == 100
              and .xpPerSubStage[1] == 200
              and .baseStatsPerSubStage.maxHp[0:2] == [100, 120]
              and .baseStatsPerSubStage.maxMana[0:2] == [50, 60]
              and .baseStatsPerSubStage.attack[0:2] == [10, 12]
              and .baseStatsPerSubStage.defense[0:2] == [5, 6])
    ' "${PROJECT_ROOT}/${realm_config}" >/dev/null

    assert_pattern 'public interface ISpiritRootService' "${root_contract}"
    assert_pattern 'bool TryChooseRoot\(SpiritRootType type\)' "${root_contract}"
    assert_pattern 'bool TryRandomizeRoot\(int seed\)' "${root_contract}"
    assert_pattern 'public interface ICultivationService' "${cultivation_contract}"
    assert_pattern 'void AddXp\(float amount, XpSourceType source\)' "${cultivation_contract}"
    assert_pattern 'bool TryAdvanceSubStage\(\)' "${cultivation_contract}"
    assert_pattern 'float CultivationSpeed' "${stats_contract}"
    assert_pattern 'XpGained = "OnXpGained"' "${cultivation_events}"

    assert_pattern 'public sealed class SpiritRootSystem : SafeBehaviour, ISpiritRootService' "${root_system}"
    assert_pattern 'ServiceLocator.Register<ISpiritRootService>' "${root_system}"
    assert_pattern 'IsDefaultPickable\(type\)' "${root_system}"
    assert_pattern 'new System.Random\(seed\)' "${root_system}"
    assert_pattern 'entry.Weight' "${root_system}"
    assert_pattern 'bonuses.TryGetValue\("All"' "${root_system}"
    assert_pattern 'Profile.SpiritRoot = type.ToString\(\)' "${root_system}"

    assert_pattern 'public sealed class CultivationManager : SafeBehaviour, ICultivationService' "${cultivation}"
    assert_pattern 'TrainingDummyXpReward = 25f' "${cultivation}"
    assert_pattern 'CombatEvents.EnemyKilled' "${cultivation}"
    assert_pattern 'CombatContentIds.TrainingDummyEnemyId' "${cultivation}"
    assert_pattern 'AddXp\(TrainingDummyXpReward, XpSourceType.Combat\)' "${cultivation}"
    assert_pattern 'spiritRoot\?\.GetCultivationMultiplier\(\) \?\? 1f' "${cultivation}"
    assert_pattern 'stats\?\.CultivationSpeed \?\? 0f' "${cultivation}"
    assert_pattern 'while \(safety-- > 0 && TryAdvanceSubStageInternal\(profile\)\)' "${cultivation}"
    assert_pattern 'profile.SubStage = SubStage \+ 1' "${cultivation}"
    assert_pattern 'CultivationEvents.XpGained' "${cultivation}"

    if rg -U -q \
        'id: G02-01\nphase: 2\nstatus: pending' \
        "${PROJECT_ROOT}/docs/10_GOALS.md" \
        && rg -q 'TryBreakthrough|CanBreakthrough|CeremonyBeat|BreakingThrough' \
            "${PROJECT_ROOT}/${cultivation}" \
            "${PROJECT_ROOT}/${cultivation_contract}"; then
        printf 'G-VS-05 contains out-of-scope major-realm breakthrough logic.\n' >&2
        exit 1
    fi

    assert_pattern 'Profile cultivation XP must be a finite non-negative value' "${save_manager}"
    assert_pattern 'Profile spirit root is outside the supported domain' "${save_manager}"
    assert_pattern 'ICultivationStatsProvider' "${player_stats}"
    assert_pattern 'RefreshRealmBaseStatsIfChanged' "${player_stats}"
    assert_pattern 'realm\?\.BaseStatsPerSubStage' "${player_stats}"
    assert_pattern 'CultivationEvents.XpGained' "${player_stats}"
    assert_pattern 'AddComponent<SpiritRootSystem>' "${scene_bootstrap}"
    assert_pattern 'AddComponent<CultivationManager>' "${scene_bootstrap}"

    assert_pattern 'public sealed class SpiritRootSelectionView : MonoBehaviour' "${root_view}"
    assert_pattern 'FiveIntroLocalizationKey = "root_intro_five"' "${root_view}"
    assert_pattern 'WasteIntroLocalizationKey = "root_intro_waste"' "${root_view}"
    assert_pattern 'HeavenIntroLocalizationKey = "root_intro_heaven"' "${root_view}"
    assert_pattern 'PickableRoots' "${root_view}"
    assert_pattern 'SuspendGameplayInput' "${root_view}"
    assert_pattern 'public sealed class CultivationHudView : MonoBehaviour' "${hud}"
    assert_pattern 'CultivationEvents.XpGained' "${hud}"
    assert_pattern '_xpFill.fillAmount' "${hud}"
    assert_pattern 'AddComponent<CultivationHudView>' "${ui_bootstrap}"
    assert_pattern 'AddComponent<SpiritRootSelectionView>' "${ui_bootstrap}"

    assert_pattern '^\| OnXpGained \| XpGainInfo \| Cultivation \| HUD, Ach \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'G-VS-05 直接启用既有 `profile.json` 字段，不新增 Schema' docs/03_DATA_LAYER.md
    assert_pattern '练功木桩击杀提供 `09§5` 的原始修为 `25`' docs/05_CULTIVATION.md
    assert_pattern 'G-VS-05 的代码优先界面包含 Order 300' docs/08_UI_META.md
    assert_pattern '^\| enemy_training_dummy \| 练功木桩 \| Normal \| — \| 30 \| 0 \| 25 \| 0 \| G-VS-05：击杀得原始修为 25，无掉落 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_root_selection_title \| 择定灵根 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_cultivation_xp \| 修为 \{0:0\}/\{1:0\} \|$' docs/09_CONTENT.md
    assert_pattern 'id: G-VS-05' docs/10_GOALS.md
    assert_pattern 'status: (implemented|done)' docs/10_GOALS.md

    assert_pattern 'CreationUiSelectsFiveElementRootOnceAndRestoresInputOnConfirm' "${tests}"
    assert_pattern 'SeededRandomCanProduceHeavenAndWasteWithSpecifiedMultipliersAndCopy' "${tests}"
    assert_pattern 'KillingTrainingDummyGrantsCultivationXpThroughCombatEvent' "${tests}"
    assert_pattern 'FullXpAdvancesQiSubstageChangesStatsAndRefreshesHud' "${tests}"
    assert_pattern 'RootAndCultivationProgressRoundTripThroughProfile' "${tests}"

    printf 'G-VS-05 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
}

run_static_validation

if [[ "${STATIC_ONLY}" == true ]]; then
    exit 0
fi

if [[ ! -x "${UNITY_EDITOR}" ]]; then
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    printf 'Install Unity 6000.5.3f1 or set UNITY_EDITOR to its executable path.\n' >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode \
    -testResults "${RESULTS_DIR}/GVS-05-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-05-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS05PlayModeTests \
    -testResults "${RESULTS_DIR}/GVS-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-05-playmode.log"

printf 'G-VS-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
