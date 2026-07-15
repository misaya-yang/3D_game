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
    "${SCRIPT_DIR}/validate_g01_03.sh" --static-only

    local formula="Assets/_Project/Scripts/Data/Config/FormulaLibrary.cs"
    local params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local cultivation_events="Assets/_Project/Scripts/Systems/Cultivation/CultivationEvents.cs"
    local cultivation_contract="Assets/_Project/Scripts/Systems/Cultivation/ICultivationService.cs"
    local cultivation="Assets/_Project/Scripts/Systems/Cultivation/CultivationManager.cs"
    local player_events="Assets/_Project/Scripts/Systems/Player/PlayerEvents.cs"
    local respawn_contract="Assets/_Project/Scripts/Systems/Player/IPlayerRespawnService.cs"
    local respawn_point="Assets/_Project/Scripts/Systems/World/RespawnPoint.cs"
    local qingshi="Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local death_view="Assets/_Project/Scripts/Systems/UI/Combat/DeathView.cs"
    local cultivation_hud="Assets/_Project/Scripts/Systems/UI/Cultivation/CultivationHudView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0104PlayModeTests.cs"

    local required_files=(
        "${formula}" "${params}" "${cultivation_events}"
        "${cultivation_contract}" "${cultivation}" "${player_events}"
        "${respawn_contract}" "${respawn_point}" "${qingshi}"
        "${player_stats}" "${death_view}" "${cultivation_hud}"
        "${ui_bootstrap}" "${tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'DeathXpPenaltyPercent = 0\.05f' "${formula}"
    assert_pattern 'OutOfCombatRecoveryDelay = 5f' "${formula}"
    assert_pattern 'OutOfCombatHpRecoveryPerSecond = 0\.02f' "${formula}"
    assert_pattern 'OutOfCombatManaRecoveryPerSecond = 0\.03f' "${formula}"
    assert_pattern 'public struct PlayerRespawnInfo' "${params}"
    assert_pattern 'public struct CultivationXpPenaltyInfo' "${params}"

    assert_pattern 'DeathXpPenaltyApplied' "${cultivation_events}"
    assert_pattern 'ApplyDeathXpPenalty\(float percent\)' "${cultivation_contract}"
    assert_pattern 'previousXp \* appliedPercent' "${cultivation}"
    assert_pattern 'CultivationEvents\.DeathXpPenaltyApplied' "${cultivation}"
    assert_pattern 'PersistProfile\(saveManager\)' "${cultivation}"

    assert_pattern 'Respawned = "OnPlayerRespawned"' "${player_events}"
    assert_pattern 'recovery_out_of_combat' "${player_events}"
    assert_pattern 'public interface IPlayerRespawnService' "${respawn_contract}"
    assert_pattern 'public sealed class RespawnPoint' "${respawn_point}"
    assert_pattern 'TryFindNearest' "${respawn_point}"
    assert_pattern 'teleport_qingshi_town' "${qingshi}"
    assert_pattern 'ConfigureRespawnPoint' "${qingshi}"

    assert_pattern 'IPlayerRespawnService,' "${player_stats}"
    assert_pattern 'RegisterCombatActivity\(\)' "${player_stats}"
    assert_pattern 'HandleDamageApplied' "${player_stats}"
    assert_pattern 'public void TickRecovery\(float deltaTime\)' "${player_stats}"
    assert_pattern 'FormulaLibrary\.OutOfCombatHpRecoveryPerSecond' "${player_stats}"
    assert_pattern 'FormulaLibrary\.OutOfCombatManaRecoveryPerSecond' "${player_stats}"
    assert_pattern 'cultivation\.ApplyDeathXpPenalty' "${player_stats}"
    assert_pattern 'TryRespawnAtNearestPoint\(\)' "${player_stats}"
    assert_pattern 'PlayerEvents\.Respawned' "${player_stats}"
    assert_pattern 'CurrentHp = MaxHp' "${player_stats}"
    assert_pattern 'CurrentMana = MaxMana' "${player_stats}"

    assert_pattern 'public sealed class DeathView' "${death_view}"
    assert_pattern 'PanelId = "panel_death"' "${death_view}"
    assert_pattern 'MessageLocalizationKey = "ui_death_revive"' "${death_view}"
    assert_pattern 'PenaltyLocalizationKey = "ui_death_xp_penalty"' "${death_view}"
    assert_pattern 'RespawnLocalizationKey = "ui_death_respawn"' "${death_view}"
    assert_pattern 'IPlayerRespawnService' "${death_view}"
    assert_pattern 'CombatEvents\.PlayerDied' "${death_view}"
    assert_pattern 'PlayerEvents\.Respawned' "${death_view}"
    assert_pattern 'CultivationEvents\.DeathXpPenaltyApplied' "${cultivation_hud}"
    assert_pattern 'AddComponent<DeathView>' "${ui_bootstrap}"

    assert_pattern 'DeathPenalizesOnceShowsUiPersistsAndRespawnsAtNearestPoint' "${tests}"
    assert_pattern 'RecoveryStartsOnlyAfterFiveSecondsAtTwoAndThreePercentPerSecond' "${tests}"
    assert_pattern 'DamageDealtResetsClockAndNonPlayingStatesDoNotRecover' "${tests}"

    assert_pattern '^\| OnPlayerRespawned \| PlayerRespawnInfo \| PlayerStats \| Death UI, Camera, Save \|$' docs/02_ARCHITECTURE.md
    assert_pattern '^\| OnDeathXpPenaltyApplied \| CultivationXpPenaltyInfo \| Cultivation \| Death UI, Cultivation HUD \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public struct PlayerRespawnInfo' docs/03_DATA_LAYER.md
    assert_pattern 'public struct CultivationXpPenaltyInfo' docs/03_DATA_LAYER.md
    assert_pattern 'OutOfCombatRecoveryDelay = 5f' docs/03_DATA_LAYER.md
    assert_pattern 'recovery_out_of_combat' docs/04_PLAYER_COMBAT.md
    assert_pattern '^### 4\.2 玩家资源来源 ID（G01-04）$' docs/09_CONTENT.md
    assert_pattern '^\| teleport_qingshi_town \| map_qingshi \|' docs/09_CONTENT.md
    assert_pattern '^\| ui_death_respawn \| 于最近传送阵复生 \|$' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G01-04\nphase: 1\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G01-04 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    if rg -q 'InputBuffer|Hitstop|HitStop|PerfectDodge|Poise' \
        "${PROJECT_ROOT}/${death_view}" \
        "${PROJECT_ROOT}/${respawn_point}"; then
        printf 'G01-04 contains out-of-scope G01-05 or Post-MVP behavior.\n' >&2
        exit 1
    fi

    printf 'G01-04 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0104PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/G01-04-playmode.log"

printf 'G01-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
