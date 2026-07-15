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
    "${SCRIPT_DIR}/validate_g00_01.sh" --static-only

    local required_files=(
        Assets/_Project/Scripts/Core/EventBus.cs
        Assets/_Project/Scripts/Core/ServiceLocator.cs
        Assets/_Project/Scripts/Core/Singleton.cs
        Assets/_Project/Tests/EditMode/Wendao.Core.EditModeTests.asmdef
        Assets/_Project/Tests/EditMode/EventBusTests.cs
        Assets/_Project/Tests/EditMode/ServiceLocatorTests.cs
        Assets/_Project/Tests/PlayMode/SingletonPlayModeTests.cs
        Assets/_Project/Tests/PlayMode/TestSingleton.cs
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    jq empty "${PROJECT_ROOT}/Assets/_Project/Tests/EditMode/Wendao.Core.EditModeTests.asmdef"
    jq empty "${PROJECT_ROOT}/Assets/_Project/Tests/PlayMode/Wendao.Project.PlayModeTests.asmdef"
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Tests/EditMode/Wendao.Core.EditModeTests.asmdef")" == '["Wendao.Core"]' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Tests/PlayMode/Wendao.Project.PlayModeTests.asmdef")" == '["Wendao.Core"]' ]]

    local event_bus="Assets/_Project/Scripts/Core/EventBus.cs"
    assert_pattern 'public static void Subscribe<T>\(string eventName, Action<T> handler\)' "${event_bus}"
    assert_pattern 'public static void Unsubscribe<T>\(string eventName, Action<T> handler\)' "${event_bus}"
    assert_pattern 'public static void Publish<T>\(string eventName, T args\)' "${event_bus}"
    assert_pattern 'public static void Subscribe\(string eventName, Action handler\)' "${event_bus}"
    assert_pattern 'public static void Unsubscribe\(string eventName, Action handler\)' "${event_bus}"
    assert_pattern 'public static void Publish\(string eventName\)' "${event_bus}"
    assert_pattern 'public static void Clear\(\)' "${event_bus}"
    [[ "$(rg -c 'catch \(Exception exception\)' "${PROJECT_ROOT}/${event_bus}")" -eq 2 ]]
    assert_pattern 'Debug\.LogException\(exception\)' "${event_bus}"

    local service_locator="Assets/_Project/Scripts/Core/ServiceLocator.cs"
    assert_pattern 'public static void Register<T>\(T service\) where T : class' "${service_locator}"
    assert_pattern 'public static void Unregister<T>\(\) where T : class' "${service_locator}"
    assert_pattern 'public static T Get<T>\(\) where T : class' "${service_locator}"
    assert_pattern 'public static bool TryGet<T>\(out T service\) where T : class' "${service_locator}"
    assert_pattern 'public static void Clear\(\)' "${service_locator}"

    local singleton="Assets/_Project/Scripts/Core/Singleton.cs"
    assert_pattern 'public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>' "${singleton}"
    assert_pattern 'DontDestroyOnLoad\(gameObject\)' "${singleton}"
    assert_pattern 'Destroy\(gameObject\)' "${singleton}"
    assert_pattern 'FindAnyObjectByType<T>\(FindObjectsInactive.Include\)' "${singleton}"

    if rg -q 'OnPlayerDamaged|OnEnemyKilled|OnRealmBreakthrough|OnQuestCompleted' \
        "${PROJECT_ROOT}/${event_bus}"; then
        printf 'EventBus contains concrete gameplay event business, which is out of scope for G00-02.\n' >&2
        exit 1
    fi

    printf 'G00-02 static validation passed.\n'
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

UNITY_EDITOR="${UNITY_EDITOR}" "${SCRIPT_DIR}/validate_g00_01.sh"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode \
    -testResults "${RESULTS_DIR}/G00-02-editmode.xml" \
    -logFile "${RESULTS_DIR}/G00-02-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.SingletonPlayModeTests \
    -testResults "${RESULTS_DIR}/G00-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G00-02-playmode.log"

printf 'G00-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
