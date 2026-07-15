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

    local brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local spawner="Assets/_Project/Scripts/Entities/Enemy/EnemySpawner.cs"
    local bootstrap="Assets/_Project/Scripts/Entities/Enemy/WolfRuntimeBootstrap.cs"
    local surface="Assets/_Project/Scripts/Systems/World/QingshiNavigationSurface.cs"
    local map="Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0401PlayModeTests.cs"

    local required_files=(
        "${brain}"
        "${spawner}"
        "${bootstrap}"
        "${surface}"
        "${map}"
        "${tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'Patrol,' "${brain}"
    assert_pattern 'Alert,' "${brain}"
    assert_pattern 'AlertDurationSeconds = 0.4f' "${brain}"
    assert_pattern 'PatrolWaitMinSeconds = 1f' "${brain}"
    assert_pattern 'PatrolWaitMaxSeconds = 2f' "${brain}"
    assert_pattern 'NavMesh.CalculatePath' "${brain}"
    assert_pattern 'NavMeshPathStatus.PathComplete' "${brain}"
    assert_pattern 'RequireComponent.*NavMeshAgent' "${brain}"
    assert_pattern 'ConfigurePatrolRoute' "${brain}"
    assert_pattern 'CurrentHp = MaxHp' "${brain}"
    assert_pattern 'Visual_Alert_Exclamation' "${brain}"

    assert_pattern 'public Vector3\[\] PatrolOffsets' "${spawner}"
    assert_pattern 'BuildWorldPatrolRoute' "${spawner}"
    assert_pattern 'SpawnAreaCount = 3' "${bootstrap}"
    assert_pattern 'TotalConfiguredAlive = 7' "${bootstrap}"
    assert_pattern 'CreekSpawnerObjectName' "${bootstrap}"
    assert_pattern 'SouthSpawnerObjectName' "${bootstrap}"

    assert_pattern 'public sealed class QingshiNavigationSurface' "${surface}"
    assert_pattern 'NavMeshBuilder.CollectSources' "${surface}"
    assert_pattern 'NavMeshBuilder.BuildNavMeshData' "${surface}"
    assert_pattern 'surface.Rebuild' "${map}"

    assert_pattern 'RuntimeNavMeshAndThreeSpawnAreasAreReady' "${tests}"
    assert_pattern 'WolfPatrolsThenShowsPointFourSecondAlertBeforeChasing' "${tests}"
    assert_pattern 'WolfUsesCompleteNavMeshPathAroundSolidObstacle' "${tests}"
    assert_pattern 'DeadTargetMakesWolfReturnHomeAndRestoreFullHealth' "${tests}"

    if ! rg -U -q \
        'id: G04-01\nphase: 4\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G04-01 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G04-01 static validation passed.\n'
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
    -testResults "${RESULTS_DIR}/G04-01-editmode.xml" \
    -logFile "${RESULTS_DIR}/G04-01-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests' \
    -testResults "${RESULTS_DIR}/G04-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G04-01-playmode.log"

printf 'G04-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
