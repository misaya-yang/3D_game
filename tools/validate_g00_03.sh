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
        printf 'Required contract not found in %s: %s\n' "${file_path}" "${pattern}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g00_02.sh" --static-only

    local game_state="Assets/_Project/Scripts/Core/GameState.cs"
    local state_info="Assets/_Project/Scripts/Core/GameStateInfo.cs"
    local game_manager="Assets/_Project/Scripts/Core/GameManager.cs"
    local tests="Assets/_Project/Tests/PlayMode/GameManagerPlayModeTests.cs"

    assert_file "${game_state}"
    assert_file "${state_info}"
    assert_file "${game_manager}"
    assert_file "${tests}"

    local state
    for state in Boot MainMenu Loading Playing Paused Dialogue Cutscene Dead; do
        assert_pattern "^[[:space:]]*${state},?$" "${game_state}"
    done

    assert_pattern 'public GameState Prev;' "${state_info}"
    assert_pattern 'public GameState Next;' "${state_info}"
    assert_pattern 'public sealed class GameManager : Singleton<GameManager>' "${game_manager}"
    assert_pattern 'public GameState State \{ get; private set; \}' "${game_manager}"
    assert_pattern 'public bool IsInCombat \{ get; private set; \}' "${game_manager}"
    assert_pattern 'public bool TrySetState\(GameState next\)' "${game_manager}"
    assert_pattern 'public void SetCombatFlag\(bool inCombat\)' "${game_manager}"
    assert_pattern 'GameStateChangedEvent = "OnGameStateChanged"' "${game_manager}"
    assert_pattern 'EventBus\.Publish\(' "${game_manager}"

    if [[ "$(rg -c '^                \[GameState\.' "${PROJECT_ROOT}/${game_manager}")" -ne 8 ]]; then
        printf 'GameManager must define one transition-table entry for each GameState.\n' >&2
        exit 1
    fi

    assert_pattern 'IllegalTransitionReturnsFalseWithoutChangingStateOrPublishing' "${tests}"
    assert_pattern 'SuccessfulTransitionPublishesPreviousAndNextState' "${tests}"
    assert_pattern 'CombatFlagChangesWithoutChangingStateOrPublishingStateEvent' "${tests}"

    if rg -q 'UnityEngine\.UI|Wendao\.UI' "${PROJECT_ROOT}/${game_manager}"; then
        printf 'G00-03 contains UI behavior, which is out of scope.\n' >&2
        exit 1
    fi

    assert_pattern '物理文件归 Core/GameState\.cs' docs/03_DATA_LAYER.md
    assert_pattern '物理文件归 Core/GameStateInfo\.cs' docs/03_DATA_LAYER.md

    printf 'G00-03 static validation passed.\n'
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

UNITY_EDITOR="${UNITY_EDITOR}" "${SCRIPT_DIR}/validate_g00_02.sh"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.GameManagerPlayModeTests \
    -testResults "${RESULTS_DIR}/G00-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G00-03-playmode.log"

printf 'G00-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"

