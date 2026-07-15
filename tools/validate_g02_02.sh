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
    "${SCRIPT_DIR}/validate_g02_01.sh" --static-only

    local contract="Assets/_Project/Scripts/Systems/Cultivation/IBodyRefinementService.cs"
    local manager="Assets/_Project/Scripts/Systems/Cultivation/BodyRefinementManager.cs"
    local reduction="Assets/_Project/Scripts/Systems/Combat/ICombatDamageReductionProvider.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local item_use="Assets/_Project/Scripts/Systems/Inventory/ItemUseSystem.cs"
    local inventory_ids="Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local config_tests="Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs"
    local playmode_tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0202PlayModeTests.cs"

    local required_files=(
        "${contract}" "${manager}" "${reduction}" "${combat}"
        "${player}" "${item_use}" "${inventory_ids}" "${config}"
        "${bootstrap}" "${config_tests}" "${playmode_tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'BodyLevel Level' "${contract}"
    assert_pattern 'float Xp' "${contract}"
    assert_pattern 'float HpBonus' "${contract}"
    assert_pattern 'float PhysicalDR' "${contract}"
    assert_pattern 'void AddBodyXp\(float amount\)' "${contract}"
    assert_pattern 'void AddBodyXpFromPotion\(float amount\)' "${contract}"
    assert_pattern 'bool TryLevelUp\(\)' "${contract}"

    assert_pattern 'CombatEvents\.PlayerDamaged' "${manager}"
    assert_pattern 'XpFromDamageTakenMul' "${manager}"
    assert_pattern 'GetBodyMultiplier\(\)' "${manager}"
    assert_pattern 'GetBodyPotionMul\(\)' "${manager}"
    assert_pattern 'nextEntry\.XpToNext' "${manager}"

    assert_pattern 'ICombatDamageReductionProvider' "${reduction}"
    assert_pattern 'targetDamageReduction\.GetDamageReduction' "${combat}"
    assert_pattern 'ICombatDamageReductionProvider' "${player}"
    assert_pattern 'ResolveBodyHpBonus' "${player}"
    assert_pattern 'fixedStats\.MaxHp \*= 1f \+ bodyHpBonus' "${player}"
    assert_pattern 'spiritRoot\.GetBlockPhysDrBonus' "${player}"
    assert_pattern 'PhysicalBlockDamageReduction \+ Mathf\.Max' "${player}"

    assert_pattern 'UseEffectType\.AddBodyXp' "${item_use}"
    assert_pattern 'body\.AddBodyXpFromPotion\(effect\.Value\)' "${item_use}"
    assert_pattern 'BodyPotion01 = "item_potion_body_01"' "${inventory_ids}"
    assert_pattern 'bodyPotion\.MaxStack = 10' "${config}"
    assert_pattern 'EffectType = UseEffectType\.AddBodyXp' "${config}"
    assert_pattern 'Value = 200f' "${config}"
    assert_pattern 'EnsureBodyRefinementManager\(\)' "${bootstrap}"
    assert_pattern 'AddComponent<BodyRefinementManager>' "${bootstrap}"

    assert_pattern 'bodyPotion\.UseEffects\[0\]\.Value' "${config_tests}"
    assert_pattern 'DamageAndBodyPotionUseApplyRootSpecificXpMultipliers' "${playmode_tests}"
    assert_pattern 'LevelUpUsesCumulativeThresholdsAndAddsHpAndPhysicalDr' "${playmode_tests}"
    assert_pattern 'WasteRootBlockingHasPointSevenDrAndTakesThreeQuartersDamage' "${playmode_tests}"

    assert_pattern '^\| item_potion_body_01 \|' docs/09_CONTENT.md
    assert_pattern '受伤 \| damageTaken \* 0\.1 \* bodyMul' docs/05_CULTIVATION.md
    assert_pattern 'blockPhysDrBonus \| 0\.10' docs/05_CULTIVATION.md

    printf 'G02-02 static validation passed.\n'
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

run_unity_tests() {
    local platform="$1"
    local filter="$2"
    local result_name="$3"
    local result_path="${RESULTS_DIR}/${result_name}.xml"
    local log_path="${RESULTS_DIR}/${result_name}.log"
    local unity_exit=0

    rm -f "${result_path}" "${log_path}"
    "${UNITY_EDITOR}" \
        -batchmode \
        -nographics \
        -projectPath "${PROJECT_ROOT}" \
        -runTests \
        -testPlatform "${platform}" \
        -testFilter "${filter}" \
        -testResults "${result_path}" \
        -logFile "${log_path}" || unity_exit=$?

    if [[ ! -f "${result_path}" ]] \
        || ! rg -q '<test-run .*result="Passed".*failed="0"' \
            "${result_path}"; then
        printf '%s validation failed (Unity exit %s). See %s\n' \
            "${platform}" "${unity_exit}" "${log_path}" >&2
        if [[ "${unity_exit}" -eq 0 ]]; then
            unity_exit=1
        fi
        return "${unity_exit}"
    fi

    if [[ "${unity_exit}" -ne 0 ]]; then
        printf '%s tests passed; ignoring Unity shutdown exit %s.\n' \
            "${platform}" "${unity_exit}"
    fi
}

run_unity_tests \
    EditMode \
    Wendao.Tests.EditMode.Data \
    G02-02-editmode

run_unity_tests \
    PlayMode \
    Wendao.Tests.PlayMode.VerticalSlice.G0202PlayModeTests \
    G02-02-playmode

printf 'G02-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
