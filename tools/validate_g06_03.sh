#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"

STATIC_ONLY=false
RUN_EDITMODE=true
case "${1:-}" in
    "") ;;
    --static-only) STATIC_ONLY=true ;;
    --playmode-only) RUN_EDITMODE=false ;;
    *) printf 'Usage: %s [--static-only|--playmode-only]\n' "$0" >&2; exit 64 ;;
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
    "${SCRIPT_DIR}/validate_g06_02.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local manager="Assets/_Project/Scripts/Systems/Mount/MountManager.cs"
    local contract="Assets/_Project/Scripts/Systems/Mount/IMountService.cs"
    local locomotion="Assets/_Project/Scripts/Systems/Player/IPlayerMountLocomotion.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local skill="Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    local save_data="Assets/_Project/Scripts/Data/Runtime/MountRuntimeData.cs"
    local no_fly="Assets/_Project/Scripts/Systems/World/NoFlyZone.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0603PlayModeTests.cs"
    local files=("${manager}" "${contract}" "${locomotion}" "${player}" "${combat}" "${skill}" "${save_data}" "${no_fly}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'SaveModuleName = "mounts"' "${manager}"
    assert_pattern 'SpiritHorseSpeedMultiplier = 1\.5f' "${manager}"
    assert_pattern 'MaximumFlightHeight = 40f' "${manager}"
    assert_pattern 'MountContentIds\.FlyingSword' "${manager}"
    assert_pattern 'RealmType\.Foundation' "${manager}"
    assert_pattern 'BlackwindDungeonSceneName' "${manager}"
    assert_pattern 'SetNoFlyZoneActive' "${manager}"
    assert_pattern 'TryRegisterSaveModule' "${manager}"
    assert_pattern 'MountEvents\.MountChanged' "${manager}"
    assert_pattern 'MountEvents\.FlightStateChanged' "${manager}"
    assert_pattern 'TickFlightMovement' "${player}"
    assert_pattern '_mountMoveSpeedMultiplier' "${player}"
    assert_pattern '_inputSource\.JumpHeld' "${player}"
    assert_pattern '_inputSource\.BlockHeld' "${player}"
    assert_pattern 'IsMounted\(\)' "${combat}"
    assert_pattern 'mounts\.IsMounted' "${skill}"
    assert_pattern 'UnlockedMountIds' "${save_data}"
    assert_pattern 'NoFlyZone' "${no_fly}"
    assert_pattern 'MountsJsonRoundTripPreservesUnlocksAndSelection' "${tests}"
    assert_pattern 'FlyingSwordRequiresFoundationAndHonorsNoFlyZones' "${tests}"

    assert_pattern 'G06-03 实现说明' docs/08_UI_META.md
    assert_pattern 'ui_mount_flight_blocked' docs/09_CONTENT.md
    assert_pattern 'MountSaveData' docs/03_DATA_LAYER.md

    if ! rg -U -q \
        'id: G06-03\nphase: 6\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G06-03 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G06-03 static validation passed.\n'
}

run_static_validation
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
if [[ "${RUN_EDITMODE}" == true ]]; then
    "${UNITY_EDITOR}" \
        -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
        -runTests -testPlatform EditMode \
        -testFilter Wendao.Tests.EditMode.Data \
        -testResults "${RESULTS_DIR}/G06-03-editmode.xml" \
        -logFile "${RESULTS_DIR}/G06-03-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0602PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0603PlayModeTests' \
    -testResults "${RESULTS_DIR}/G06-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G06-03-playmode.log"

printf 'G06-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
