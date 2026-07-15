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
    "${SCRIPT_DIR}/validate_g06_05.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local world="Assets/_Project/Scripts/Systems/World"
    local files=(
        "${world}/WorldEnvironmentEvents.cs"
        "${world}/IDayNightService.cs"
        "${world}/DayNightSystem.cs"
        "${world}/IWeatherService.cs"
        "${world}/WeatherSystem.cs"
        "${world}/WorldEnvironmentProfiles.cs"
        "Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
        "Assets/_Project/Scripts/Entities/Player/PlayerTargetingController.cs"
        "Assets/_Project/Scripts/Data/Save/SaveManager.cs"
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0701PlayModeTests.cs"
    )
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'DefaultCycleDurationSeconds = 48f \* 60f' "${world}/DayNightSystem.cs"
    assert_pattern 'SunriseHour = 6f' "${world}/DayNightSystem.cs"
    assert_pattern 'SunsetHour = 18f' "${world}/DayNightSystem.cs"
    assert_pattern 'World\.TimeOfDay|_boundWorld\.TimeOfDay' "${world}/DayNightSystem.cs"
    assert_pattern 'WorldEnvironmentEvents\.DayNightChanged' "${world}/DayNightSystem.cs"
    assert_pattern 'MinimumDurationSeconds = 120f' "${world}/WeatherSystem.cs"
    assert_pattern 'MaximumDurationSeconds = 300f' "${world}/WeatherSystem.cs"
    assert_pattern 'TransitionDurationSeconds = 3f' "${world}/WeatherSystem.cs"
    assert_pattern 'RainElementBonus = 0\.1f' "${world}/WeatherSystem.cs"
    assert_pattern 'FogVisionMultiplier = 0\.75f' "${world}/WeatherSystem.cs"
    assert_pattern 'WorldEnvironmentEvents\.WeatherChanged' "${world}/WeatherSystem.cs"
    assert_pattern 'QingshiWeather' "${world}/WorldEnvironmentProfiles.cs"
    assert_pattern 'CangwuWeather' "${world}/WorldEnvironmentProfiles.cs"
    assert_pattern 'BlackwindWeather' "${world}/WorldEnvironmentProfiles.cs"
    assert_pattern 'weather\.GetElementDamageBonus' "Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    assert_pattern 'dayNight\.EnemyAttackMultiplier' "Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    assert_pattern 'weather\.GetVisionMul' "Assets/_Project/Scripts/Entities/Player/PlayerTargetingController.cs"
    assert_pattern 'World time of day must be in the range' "Assets/_Project/Scripts/Data/Save/SaveManager.cs"
    assert_pattern 'TimeOfDayPersistsThroughWorldSaveRoundTrip' "Assets/_Project/Tests/PlayMode/VerticalSlice/G0701PlayModeTests.cs"
    assert_pattern 'CombatUsesRainElementBonusAndNightEnemyMultiplier' "Assets/_Project/Tests/PlayMode/VerticalSlice/G0701PlayModeTests.cs"

    assert_pattern 'G07-01 运行时实现' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'G07-01 world 时间约束' docs/03_DATA_LAYER.md
    if ! rg -U -q \
        'id: G07-01\nphase: 7\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G07-01 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G07-01 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G07-01-editmode.xml" \
        -logFile "${RESULTS_DIR}/G07-01-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0102PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0103PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0302PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0501PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0603PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0701PlayModeTests' \
    -testResults "${RESULTS_DIR}/G07-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G07-01-playmode.log"

printf 'G07-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
