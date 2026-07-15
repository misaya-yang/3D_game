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
    "${SCRIPT_DIR}/validate_g02_03.sh" --static-only

    local profile="Assets/_Project/Scripts/Data/Save/SaveData.cs"
    local save_manager="Assets/_Project/Scripts/Data/Save/SaveManager.cs"
    local body="Assets/_Project/Scripts/Systems/Cultivation/BodyRefinementManager.cs"
    local stats_contract="Assets/_Project/Scripts/Systems/Player/IPlayerCharacterStatsService.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local panel="Assets/_Project/Scripts/Systems/UI/Cultivation/CharacterPanelView.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0204PlayModeTests.cs"

    for file_path in \
        "${profile}" \
        "${save_manager}" \
        "${body}" \
        "${stats_contract}" \
        "${player}" \
        "${input_contract}" \
        "${input_reader}" \
        "${panel}" \
        "${bootstrap}" \
        "${tests}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'public int BodyLevel;' "${profile}"
    assert_pattern 'public float BodyXp;' "${profile}"
    assert_pattern 'Profile body level is outside the supported domain' "${save_manager}"
    assert_pattern 'Profile body XP must be a finite non-negative value' "${save_manager}"
    assert_pattern 'profile\?\.BodyLevel' "${body}"
    assert_pattern 'Profile\?\.BodyXp' "${body}"
    assert_pattern 'public float XpToNext' "${body}"
    assert_pattern 'PersistProfile\(\);' "${body}"

    assert_pattern 'interface IPlayerCharacterStatsService' "${stats_contract}"
    assert_pattern 'IPlayerCharacterStatsService' "${player}"
    assert_pattern 'Register<IPlayerCharacterStatsService>' "${player}"

    assert_pattern 'OpenCharacterPressedThisFrame' "${input_contract}"
    assert_pattern 'FindAction\("OpenCharacter", true\)' "${input_reader}"
    assert_pattern 'OpenCharacterPressedThisFrame' "${panel}"
    assert_pattern 'public sealed class CharacterPanelView' "${panel}"
    assert_pattern 'public bool TryBreakthrough\(\)' "${panel}"
    assert_pattern 'SetOpen\(false\);' "${panel}"
    assert_pattern 'AddComponent<CharacterPanelView>' "${bootstrap}"

    assert_pattern 'CharacterPanelHasCBindingAndDisplaysRealmRootBodyAndFinalStats' "${tests}"
    assert_pattern 'BreakthroughButtonClosesPanelAndStartsReadyCeremony' "${tests}"
    assert_pattern 'BodyLevelAndXpRoundTripThroughProfileWithoutShadowState' "${tests}"

    assert_pattern '"bodyLevel": 0' docs/02_ARCHITECTURE.md
    assert_pattern 'G02-04 在 schema v1 上向后兼容追加' docs/03_DATA_LAYER.md
    assert_pattern 'G02-04 增加 Order 210' docs/08_UI_META.md
    assert_pattern 'ui_character_breakthrough_ready' docs/09_CONTENT.md

    printf 'G02-04 static validation passed.\n'
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
    G02-04-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.G0202PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0203PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0204PlayModeTests' \
    G02-04-playmode

printf 'G02-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
