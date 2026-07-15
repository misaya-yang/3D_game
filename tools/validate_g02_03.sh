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
    "${SCRIPT_DIR}/validate_g02_02.sh" --static-only

    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local combat_contract="Assets/_Project/Scripts/Systems/Combat/ICombatStatsProvider.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0203PlayModeTests.cs"

    assert_file "${player}"
    assert_file "${combat_contract}"
    assert_file "${tests}"

    assert_pattern 'StatBlock BaseFromRealm' "${player}"
    assert_pattern 'StatBlock FromEquipment' "${player}"
    assert_pattern 'StatBlock FromTitle' "${player}"
    assert_pattern 'StatBlock FromBuffs' "${player}"
    assert_pattern 'StatBlock Final' "${player}"
    assert_pattern 'public void Recalculate\(\)' "${player}"
    assert_pattern 'public void SetTitleBonus\(StatBlock bonus\)' "${player}"
    assert_pattern 'public void SetBuffBonus\(StatBlock bonus\)' "${player}"
    assert_pattern 'StatBlock fixedStats = BaseFromRealm' "${player}"
    assert_pattern '^                    \+ FromEquipment' "${player}"
    assert_pattern '^                    \+ FromTitle' "${player}"
    assert_pattern 'fixedStats\.MaxHp \*= 1f \+ bodyHpBonus' "${player}"
    assert_pattern 'StatBlock result = fixedStats \+ FromBuffs' "${player}"
    assert_pattern 'public float Attack =>.*Final' "${player}"
    assert_pattern 'public float Defense =>.*Final' "${player}"
    assert_pattern 'public float CritRate =>.*Final' "${player}"
    assert_pattern 'public float CritDamage =>.*Final' "${player}"
    assert_pattern 'HandleEquipmentChanged' "${player}"
    assert_pattern 'BuildSpiritRootStats' "${player}"

    assert_pattern 'fully aggregated PlayerStats result' "${combat_contract}"
    assert_pattern 'RealmEquipmentTitleAndBuffSourcesAggregateInDocumentedOrder' "${tests}"
    assert_pattern 'RecalculateRefreshesFinalAndCombatConsumesFinalAttackAndDefense' "${tests}"
    assert_pattern 'BodyHpPercentAppliesAfterFixedSourcesAndBeforeFlatBuffs' "${tests}"

    assert_pattern '任何来源变化必须 `Recalculate\(\)`' docs/05_CULTIVATION.md
    assert_pattern 'Final = \(RealmBase \+ Equipment \+ Title' docs/05_CULTIVATION.md

    printf 'G02-03 static validation passed.\n'
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
    G02-03-editmode

run_unity_tests \
    PlayMode \
    Wendao.Tests.PlayMode.VerticalSlice.G0203PlayModeTests \
    G02-03-playmode

printf 'G02-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
