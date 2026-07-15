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
    "${SCRIPT_DIR}/validate_gvs_07.sh" --static-only

    local result="Assets/_Project/Scripts/Systems/Debugging/DebugCommandResult.cs"
    local console_contract="Assets/_Project/Scripts/Systems/Debugging/IDebugConsoleService.cs"
    local player_contract="Assets/_Project/Scripts/Systems/Debugging/IDebugPlayerService.cs"
    local enemy_contract="Assets/_Project/Scripts/Systems/Enemy/IEnemySpawnService.cs"
    local console="Assets/_Project/Scripts/Systems/Debugging/DebugConsoleService.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local enemy_spawner="Assets/_Project/Scripts/Entities/Enemy/EnemySpawner.cs"
    local enemy_service="Assets/_Project/Scripts/Entities/Enemy/EnemySpawnService.cs"
    local wolf_bootstrap="Assets/_Project/Scripts/Entities/Enemy/WolfRuntimeBootstrap.cs"
    local view="Assets/_Project/Scripts/Systems/UI/Debugging/DebugConsoleView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS08PlayModeTests.cs"
    local checklist="docs/GVS08_VERTICAL_SLICE_CHECKLIST.md"

    local required_files=(
        "${result}"
        "${console_contract}"
        "${player_contract}"
        "${enemy_contract}"
        "${console}"
        "${scene_bootstrap}"
        "${player}"
        "${player_stats}"
        "${enemy_spawner}"
        "${enemy_service}"
        "${wolf_bootstrap}"
        "${view}"
        "${ui_bootstrap}"
        "${tests}"
        "${checklist}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern '^#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR$' "${console}"
    assert_pattern '^#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR$' "${view}"
    assert_pattern 'public interface IDebugConsoleService' "${console_contract}"
    assert_pattern 'DebugCommandResult Execute\(string commandLine\);' "${console_contract}"
    assert_pattern 'public sealed class DebugConsoleService : MonoBehaviour, IDebugConsoleService' "${console}"
    assert_pattern 'ServiceLocator.Register<IDebugConsoleService>' "${console}"
    assert_pattern 'case "/god":' "${console}"
    assert_pattern 'case "/killall":' "${console}"
    assert_pattern 'case "/setrealm":' "${console}"
    assert_pattern 'case "/givexp":' "${console}"
    assert_pattern 'case "/give":' "${console}"
    assert_pattern 'case "/spawn":' "${console}"
    assert_pattern 'case "/tp":' "${console}"
    assert_pattern 'case "/save":' "${console}"
    assert_pattern 'case "/timescale":' "${console}"
    assert_pattern 'case "/tutorial_skip":' "${console}"
    assert_pattern 'AcquireSource.Cheat' "${console}"
    assert_pattern 'cultivation.AddXp\(amount, XpSourceType.Other\)' "${console}"
    assert_pattern 'saveManager.SaveGame\(slot\)' "${console}"
    assert_pattern 'tutorials.TryStart\(tutorialId\)' "${console}"
    assert_pattern 'tutorials.Skip\(\)' "${console}"
    assert_pattern 'HasCompleted\(tutorialId\)' "${console}"

    assert_pattern 'public interface IEnemySpawnService' "${enemy_contract}"
    assert_pattern 'bool TrySpawn\(string enemyId, int count, out int spawnedCount\);' "${enemy_contract}"
    assert_pattern 'public sealed class EnemySpawnService : MonoBehaviour, IEnemySpawnService' "${enemy_service}"
    assert_pattern 'EnemySpawner.CreateRuntimeEnemy' "${enemy_service}"
    assert_pattern 'combat.DealDamage' "${enemy_service}"
    assert_pattern 'AddComponent<EnemySpawnService>' "${wolf_bootstrap}"
    assert_pattern 'public static EnemyBrain CreateRuntimeEnemy' "${enemy_spawner}"

    assert_pattern 'PlayerController : SafeBehaviour, IDebugPlayerService' "${player}"
    assert_pattern '#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR' "${player}"
    assert_pattern 'public bool SetGodMode\(bool enabled\)' "${player}"
    assert_pattern 'public bool IsInvincible' "${player_stats}"

    assert_pattern 'EnsureDebugConsoleService\(\)' "${scene_bootstrap}"
    assert_pattern 'gameManager.gameObject.AddComponent<DebugConsoleService>' "${scene_bootstrap}"
    assert_pattern 'public sealed class DebugConsoleView : MonoBehaviour' "${view}"
    assert_pattern 'Keyboard.current' "${view}"
    assert_pattern 'backquoteKey.wasPressedThisFrame' "${view}"
    assert_pattern '_playerInput\?\.SetEnabled\(false\)' "${view}"
    assert_pattern 'gameToast.gameObject.AddComponent<DebugConsoleView>' "${ui_bootstrap}"

    assert_pattern '^\| /tutorial_skip \|' docs/02_ARCHITECTURE.md
    assert_pattern 'IEnemySpawnService' docs/02_ARCHITECTURE.md
    assert_pattern '^\| ui_debug_console_title \|' docs/09_CONTENT.md
    assert_pattern '^\| debug_console_success_tutorial_skip \|' docs/09_CONTENT.md
    assert_pattern '代码优先核对（2026-07-15）' docs/10_GOALS.md
    assert_pattern '^\| 8 \| 存读档后三态一致' "${checklist}"
    assert_pattern '^- \[ \] Development Build / Editor' "${checklist}"

    if ! rg -U -q \
        'id: G-VS-08\nphase: VS\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G-VS-08 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    assert_pattern 'CommonDevelopmentCommandsExecuteThroughServiceBoundaries' "${tests}"
    assert_pattern 'TutorialSkipWritesTheSameCompletionKeysAndSurvivesLoad' "${tests}"
    assert_pattern 'SaveRoundTripPreservesQuestInventoryAndTutorialTogether' "${tests}"
    assert_pattern 'ConsoleUiLocksGameplayInputAndShowsLocalizedCommandResult' "${tests}"

    printf 'G-VS-08 static validation passed. Unity compilation and vertical-slice runtime sign-off remain pending.\n'
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
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode \
    -testResults "${RESULTS_DIR}/GVS-08-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-08-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS08PlayModeTests \
    -testResults "${RESULTS_DIR}/GVS-08-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-08-playmode.log"

printf 'G-VS-08 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
