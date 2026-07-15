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
    *) printf 'Usage: %s [--static-only]\n' "$0" >&2; exit 64 ;;
esac

assert_file() {
    [[ -f "${PROJECT_ROOT}/$1" ]] || {
        printf 'Required file missing: %s\n' "$1" >&2
        exit 1
    }
}

assert_pattern() {
    local pattern="$1"
    local file_path="$2"
    rg -q "${pattern}" "${PROJECT_ROOT}/${file_path}" || {
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    }
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g04_05.sh" --static-only

    local factory="Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    local marker="Assets/_Project/Scripts/Systems/World/WorldAreaMarker.cs"
    local zone="Assets/_Project/Scripts/Systems/World/SafeZone.cs"
    local zone_system="Assets/_Project/Scripts/Systems/World/SafeZoneSystem.cs"
    local zone_api="Assets/_Project/Scripts/Systems/World/ISafeZoneService.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0501PlayModeTests.cs"
    local files=("${factory}" "${marker}" "${zone}" "${zone_system}" "${zone_api}" "${scene_bootstrap}" "${brain}" "${combat}" "${player}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'RequiredAreaCount = 5' "${factory}"
    assert_pattern 'RequiredChestCount = 2' "${factory}"
    assert_pattern 'Area_QingshiTown' "${factory}"
    assert_pattern 'Area_TrainingGround' "${factory}"
    assert_pattern 'Area_EastWilderness' "${factory}"
    assert_pattern 'Area_HerbCreek' "${factory}"
    assert_pattern 'Area_SecretPathEntrance' "${factory}"
    assert_pattern 'safezone_qingshi_town_training' "${factory}"
    assert_pattern 'public sealed class WorldAreaMarker' "${marker}"
    assert_pattern 'public sealed class SafeZone' "${zone}"
    assert_pattern 'DefaultRecoveryMultiplier = 2f' "${zone}"
    assert_pattern 'public sealed class SafeZoneSystem' "${zone_system}"
    assert_pattern 'EnsureSafeZoneSystem' "${scene_bootstrap}"
    assert_pattern 'IsPositionInsideSafeZone' "${brain}"
    assert_pattern 'IsBlockedBySafeZone' "${combat}"
    assert_pattern 'GetRecoveryMultiplier' "${player}"

    assert_pattern 'QingshiHasFiveDistinctLocalizedAreasAndTwoChests' "${tests}"
    assert_pattern 'TownAndTrainingGroundAreSafeButWildernessIsNot' "${tests}"
    assert_pattern 'EnemyCannotAggroSafePlayerAndReturnsWhenPlayerEntersZone' "${tests}"
    assert_pattern 'SafeZoneBlocksEnemyDamageAndDoublesRecoveryRate' "${tests}"
    assert_pattern 'area_name_qingshi_town' docs/09_CONTENT.md
    assert_pattern 'safezone_qingshi_town_training' docs/07_WORLD_ENEMY_QUEST.md

    if ! rg -U -q \
        'id: G05-01\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-01 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-01 static validation passed.\n'
}

run_static_validation
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G05-01-editmode.xml" \
    -logFile "${RESULTS_DIR}/G05-01-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0304PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0402PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0501PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-01-playmode.log"

printf 'G05-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
