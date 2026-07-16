#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"
readonly JOURNEY_RESULTS_DIR="${RESULTS_DIR}/G09-03"
readonly PLAYER_APP="${PROJECT_ROOT}/Builds/macOS/WendaoChangsheng.app"
readonly PLAYER_BINARY="${PLAYER_APP}/Contents/MacOS/问道长生"

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

run_journey() {
    local run_id="$1"
    local run_root
    run_root="$(mktemp -d "${TMPDIR:-/tmp}/wendao-g09-03-${run_id}.XXXXXX")"
    mkdir -p "${run_root}/storage" "${run_root}/captures"

    "${PLAYER_BINARY}" \
        -screen-fullscreen 0 \
        -wendaoJourneyAudit \
        -wendaoJourneyClean 1 \
        -wendaoJourneyRunId "${run_id}" \
        -wendaoJourneyStorage "${run_root}/storage" \
        -wendaoJourneyReport \
            "${JOURNEY_RESULTS_DIR}/journey-${run_id}.json" \
        -wendaoJourneyCaptureDir "${run_root}/captures" \
        -logFile "${JOURNEY_RESULTS_DIR}/journey-${run_id}.log"

    jq -e \
        '.result == "Passed"
         and .error == ""
         and (.steps | length) == 55
         and all(.steps[]; .passed == true)' \
        "${JOURNEY_RESULTS_DIR}/journey-${run_id}.json" >/dev/null

    if rg -q \
        'NullReferenceException|Unhandled Exception|ConfigDatabase entered safe mode|Assertion|Exception:' \
        "${JOURNEY_RESULTS_DIR}/journey-${run_id}.log"; then
        printf 'Journey log contains a blocking runtime error: %s\n' \
            "${run_id}" >&2
        exit 1
    fi
}

cd "${PROJECT_ROOT}"
"${SCRIPT_DIR}/check_docs_consistency.sh"
bash -n "${SCRIPT_DIR}/validate_g09_03.sh"

required_files=(
    Assets/_Project/Resources/UI/G09/Icons/checkmark.png
    Assets/_Project/Resources/UI/G09/Icons/exclamation.png
    Assets/_Project/Resources/UI/G09/Icons/target.png
    Assets/_Project/Scripts/Entities/Visuals/AlchemyFurnaceRuntimeBootstrap.cs
    Assets/_Project/Scripts/Systems/Crafting/AlchemyFurnaceInteractable.cs
    Assets/_Project/Scripts/Systems/Quest/MainQuestGuidance.cs
    Assets/_Project/Scripts/Systems/UI/Quest/QuestWorldMarkerView.cs
    Assets/_Project/Scripts/Systems/UI/SceneFlow/G0903JourneyAuditLauncher.cs
    Assets/_Project/Tests/PlayMode/VerticalSlice/G0903PlayModeTests.cs
    docs/optimization/G09-03_PLAYABILITY_AUDIT.md
    docs/optimization/ISSUE_LEDGER.md
)
for file_path in "${required_files[@]}"; do
    assert_file "${file_path}"
done

[[ "$(find docs/optimization/previews/g09-03/before \
    -type f -name '*.png' | wc -l | tr -d ' ')" -ge 8 ]] || {
    printf 'Expected at least eight G09-03 baseline screenshots.\n' >&2
    exit 1
}
[[ "$(find docs/optimization/previews/g09-03/after \
    -type f -name '*.png' | wc -l | tr -d ' ')" -ge 3 ]] || {
    printf 'Expected at least three G09-03 repaired screenshots.\n' >&2
    exit 1
}

assert_pattern 'class MainQuestGuidance' \
    Assets/_Project/Scripts/Systems/Quest/MainQuestGuidance.cs
assert_pattern 'class QuestWorldMarkerView' \
    Assets/_Project/Scripts/Systems/UI/Quest/QuestWorldMarkerView.cs
assert_pattern 'class AlchemyFurnaceInteractable' \
    Assets/_Project/Scripts/Systems/Crafting/AlchemyFurnaceInteractable.cs
assert_pattern 'furnace_qingshi_town' \
    Assets/_Project/Scripts/Entities/Visuals/AlchemyFurnaceRuntimeBootstrap.cs
assert_pattern 'OnAlchemyFurnaceInteracted' \
    Assets/_Project/Scripts/Systems/Crafting/AlchemyEvents.cs
assert_pattern '<Gamepad>/select' \
    Assets/_Project/Resources/Input/PlayerInputActions.inputactions
assert_pattern '<Gamepad>/start' \
    Assets/_Project/Resources/Input/PlayerInputActions.inputactions
assert_pattern 'Screen\.SetResolution\(1280, 720, false\)' \
    Assets/_Project/Scripts/Systems/UI/SceneFlow/G0903JourneyAuditLauncher.cs
assert_pattern 'PlayerSettings\.resizableWindow = true' \
    Assets/_Project/Editor/WendaoBuild.cs
assert_pattern 'PlayerSettings\.fullScreenMode = FullScreenMode\.Windowed' \
    Assets/_Project/Editor/WendaoBuild.cs
assert_pattern 'resizableWindow: 1' \
    ProjectSettings/ProjectSettings.asset
assert_pattern 'fullscreenMode: 3' \
    ProjectSettings/ProjectSettings.asset

if rg -q '<Gamepad>/(selectButton|startButton)' \
    Assets/_Project/Resources/Input/PlayerInputActions.inputactions; then
    printf 'Legacy invalid Gamepad control paths are still present.\n' >&2
    exit 1
fi
if ! rg -U -q \
    'id: G09-03\nphase: 9\nstatus: (in_progress|implemented|done)' \
    docs/10_GOALS.md; then
    printf 'G09-03 must be active or delivered in docs/10_GOALS.md.\n' >&2
    exit 1
fi

printf 'G09-03 static validation passed.\n'
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' \
        "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}" "${JOURNEY_RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0903PlayModeTests' \
    -testResults "${RESULTS_DIR}/G09-03-targeted-playmode.xml" \
    -logFile "${RESULTS_DIR}/G09-03-targeted-playmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testResults "${RESULTS_DIR}/G09-03-full-editmode.xml" \
    -logFile "${RESULTS_DIR}/G09-03-full-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testResults "${RESULTS_DIR}/G09-03-full-playmode.xml" \
    -logFile "${RESULTS_DIR}/G09-03-full-playmode.log"

"${SCRIPT_DIR}/build_macos.sh"
"${SCRIPT_DIR}/smoke_macos_player.sh"
[[ -x "${PLAYER_BINARY}" ]] || {
    printf 'Built Player binary missing: %s\n' "${PLAYER_BINARY}" >&2
    exit 1
}

run_journey run-1
run_journey run-2
run_journey run-3

printf 'G09-03 Unity and three-run journey validation passed. Results are in %s.\n' \
    "${RESULTS_DIR}"
