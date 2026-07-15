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
    "${SCRIPT_DIR}/validate_g05_02.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/World/MapContentIds.cs"
    local dungeon="Assets/_Project/Scripts/Systems/World/BlackwindDungeonSystem.cs"
    local factory="Assets/_Project/Scripts/Systems/World/BlackwindDungeonFactory.cs"
    local gate="Assets/_Project/Scripts/Systems/World/BlackwindDungeonGate.cs"
    local spring="Assets/_Project/Scripts/Systems/World/BlackwindHealingSpring.cs"
    local encounter="Assets/_Project/Scripts/Entities/Enemy/BlackwindDungeonEncounterController.cs"
    local bootstrap="Assets/_Project/Scripts/Entities/Enemy/BlackwindDungeonRuntimeBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0503PlayModeTests.cs"
    local scene="Assets/_Project/Scenes/Dungeons/Dungeon_Blackwind.unity"
    local files=("${ids}" "${dungeon}" "${factory}" "${gate}" "${spring}" "${encounter}" "${bootstrap}" "${tests}" "${scene}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'BlackwindMap = "map_blackwind"' "${ids}"
    assert_pattern 'MaximumCheckpoint = 4' "${dungeon}"
    assert_pattern 'checkpoint \+ 1' "${dungeon}"
    assert_pattern 'Mathf.Max\(Checkpoint, floor\)' "${dungeon}"
    assert_pattern 'IsHealingSpringUsed = false' "${dungeon}"
    assert_pattern 'FloorCount = 5' "${factory}"
    assert_pattern 'Blackwind_B1_PressurePlate' "${factory}"
    assert_pattern 'Blackwind_B3_BranchChest' "${factory}"
    assert_pattern 'Blackwind_B3_SpikeHazard' "${factory}"
    assert_pattern 'Blackwind_B4_HealingSpring' "${factory}"
    assert_pattern 'Blackwind_B5_BossArena' "${factory}"
    assert_pattern 'RealmType.GoldenCore' "${gate}"
    assert_pattern 'HealFraction = 0.5f' "${spring}"
    assert_pattern 'FirstFloorWaveCount = 2' "${encounter}"
    assert_pattern 'FourthFloorEnemyCount = 4' "${encounter}"
    assert_pattern 'EnemyContentIds.StoneGeneral' "${encounter}"
    assert_pattern 'Dungeon_Blackwind' ProjectSettings/EditorBuildSettings.asset

    assert_pattern 'GoldenCoreGateBlocksFoundationAndLoadsB1ForGoldenCore' "${tests}"
    assert_pattern 'FiveFloorRuntimeFlowSpawnsMechanicsAndDefeatsBoss' "${tests}"
    assert_pattern 'CompletingB2PersistsTwoAndReentryStartsAtB3' "${tests}"
    assert_pattern 'B5FailureKeepsCheckpointAndSpringResetsPerRun' "${tests}"
    assert_pattern 'OnBlackwindFloorCompleted' docs/02_ARCHITECTURE.md
    assert_pattern 'spring_blackwind_b4_heal' docs/09_CONTENT.md
    assert_pattern 'B5 只在石将军死亡后' docs/07_WORLD_ENEMY_QUEST.md

    if ! rg -U -q \
        'id: G05-03\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-03 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-03 static validation passed.\n'
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
    -testResults "${RESULTS_DIR}/G05-03-editmode.xml" \
    -logFile "${RESULTS_DIR}/G05-03-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.SceneFlow.SceneLoaderPlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0403PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0405PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0502PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0503PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-03-playmode.log"

printf 'G05-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
