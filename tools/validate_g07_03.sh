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
    "${SCRIPT_DIR}/validate_g07_02.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local files=(
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
        "Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
        "Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
        "Assets/_Project/Scripts/Systems/World/BlackwindDungeonFactory.cs"
        "Assets/_Project/Scripts/Systems/World/WorldAreaMarker.cs"
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0703PlayModeTests.cs"
    )
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'class MvpBoundaryCatalog' \
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
    assert_pattern 'StabilityDurationSeconds = 60f \* 60f' \
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
    assert_pattern 'MinimumFramesPerSecond = 30f' \
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
    assert_pattern 'MaximumAllocatedBytes = 6L' \
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
    assert_pattern 'Application\.logMessageReceived' \
        "Assets/_Project/Scripts/Systems/Diagnostics/MvpRuntimeDiagnostics.cs"
    assert_pattern 'current != null && current\.name == materialName' \
        "Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    assert_pattern 'current != null && current\.name == materialName' \
        "Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
    assert_pattern 'current != null && current\.name == materialName' \
        "Assets/_Project/Scripts/Systems/World/BlackwindDungeonFactory.cs"
    assert_pattern 'current != null && current\.name == materialName' \
        "Assets/_Project/Scripts/Systems/World/WorldAreaMarker.cs"
    assert_pattern 'BoundaryMatrixRejectsUnsafeOperationsWithoutCrashing' \
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0703PlayModeTests.cs"
    assert_pattern 'AcceleratedSixtyMinuteSystemsSoakHasNoErrors' \
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0703PlayModeTests.cs"
    assert_pattern 'Qingshi1080pMediumProxyMeetsPerformanceBudget' \
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0703PlayModeTests.cs"

    assert_pattern 'G07-03 边界与性能证据' docs/01_VISION_MVP.md
    assert_pattern '620\.3fps' docs/01_VISION_MVP.md
    if ! rg -U -q \
        'id: G07-03\nphase: 7\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G07-03 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G07-03 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G07-03-editmode.xml" \
        -logFile "${RESULTS_DIR}/G07-03-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0201PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0301PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0302PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0404PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.GVS08PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0701PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0702PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0703PlayModeTests' \
    -testResults "${RESULTS_DIR}/G07-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G07-03-playmode.log"

"${UNITY_EDITOR}" \
    -batchmode -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0703PlayModeTests.Qingshi1080pMediumProxyMeetsPerformanceBudget \
    -testResults "${RESULTS_DIR}/G07-03-performance.xml" \
    -logFile "${RESULTS_DIR}/G07-03-performance.log"

printf 'G07-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
