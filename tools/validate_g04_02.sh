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
    "${SCRIPT_DIR}/validate_g04_01.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/Enemy/EnemyContentIds.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local spawner="Assets/_Project/Scripts/Entities/Enemy/EnemySpawner.cs"
    local bootstrap="Assets/_Project/Scripts/Entities/Enemy/EliteWolfRuntimeBootstrap.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local edit_tests="Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs"
    local play_tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0402PlayModeTests.cs"

    local required_files=(
        "${ids}"
        "${config}"
        "${brain}"
        "${spawner}"
        "${bootstrap}"
        "${player_bootstrap}"
        "${edit_tests}"
        "${play_tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'EliteWolf = "enemy_wolf_elite"' "${ids}"
    assert_pattern 'EliteWolfCharge = "skill_enemy_wolf_elite_charge"' "${ids}"
    assert_pattern 'eliteWolf.MaxHp = 800f' "${config}"
    assert_pattern 'eliteWolf.Attack = 28f' "${config}"
    assert_pattern 'eliteWolf.CultivationXpReward = 120f' "${config}"
    assert_pattern 'ItemId = "item_mat_beast_core_1"' "${config}"
    assert_pattern 'DropChance = 1f' "${config}"
    assert_pattern '"skill_enemy_wolf_elite_charge"' "${config}"

    assert_pattern 'Skill,' "${brain}"
    assert_pattern 'EliteChargeWindupSeconds = 0.4f' "${brain}"
    assert_pattern 'EliteChargeDurationSeconds = 0.7f' "${brain}"
    assert_pattern 'EliteChargeSpeedMultiplier = 2.4f' "${brain}"
    assert_pattern 'EliteChargeDamageMultiplier = 1.5f' "${brain}"
    assert_pattern 'EliteChargeCooldownSeconds = 6f' "${brain}"
    assert_pattern 'TryBeginEliteCharge' "${brain}"
    assert_pattern 'SkillId = ActiveSkillId' "${brain}"

    assert_pattern 'EliteWolfVisual_Greybox' "${spawner}"
    assert_pattern 'public static class EliteWolfRuntimeBootstrap' "${bootstrap}"
    assert_pattern 'Spawner_EliteWolf_HerbCreek' "${bootstrap}"
    assert_pattern 'EnemyContentIds.EliteWolf' "${bootstrap}"
    assert_pattern '15f' "${bootstrap}"
    assert_pattern 'EliteWolfRuntimeBootstrap.Install' "${player_bootstrap}"
    assert_pattern 'EliteWolfRuntimeBootstrap.EnsureForScene' "${player_bootstrap}"

    assert_pattern 'GetEnemy\("enemy_wolf_elite"\)' "${edit_tests}"
    assert_pattern 'EliteWolfContentSpawnAndVisualMatchSpec' "${play_tests}"
    assert_pattern 'EliteChargeClosesDistanceDealsSkillDamageAndStartsCooldown' "${play_tests}"
    assert_pattern '^\| enemy_name_enemy_wolf_elite \| 灰爪 \|$' docs/09_CONTENT.md
    assert_pattern 'skill_enemy_wolf_elite_charge' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G04-02\nphase: 4\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G04-02 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G04-02 static validation passed.\n'
}

run_static_validation

if [[ "${STATIC_ONLY}" == true ]]; then
    exit 0
fi

if [[ ! -x "${UNITY_EDITOR}" ]]; then
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G04-02-editmode.xml" \
    -logFile "${RESULTS_DIR}/G04-02-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0402PlayModeTests' \
    -testResults "${RESULTS_DIR}/G04-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G04-02-playmode.log"

printf 'G04-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
