#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly APP_PATH="${WENDAO_BUILD_PATH:-${PROJECT_ROOT}/Builds/macOS/WendaoChangsheng.app}"
readonly EXECUTABLE="${APP_PATH}/Contents/MacOS/问道长生"
readonly LOG_PATH="${PROJECT_ROOT}/TestResults/G07-04-player-smoke.log"
readonly REPORT_PATH="${PROJECT_ROOT}/TestResults/G07-04-smoke.json"

[[ -x "${EXECUTABLE}" ]] || {
    printf 'Built player executable missing: %s\n' "${EXECUTABLE}" >&2
    exit 2
}

mkdir -p "${PROJECT_ROOT}/TestResults"
rm -f "${LOG_PATH}"
"${EXECUTABLE}" -batchmode -nographics -logFile "${LOG_PATH}" >/dev/null 2>&1 &
player_pid=$!

cleanup() {
    if kill -0 "${player_pid}" 2>/dev/null; then
        kill "${player_pid}" 2>/dev/null || true
        wait "${player_pid}" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

deadline=$((SECONDS + 15))
while (( SECONDS < deadline )); do
    if [[ -f "${LOG_PATH}" ]] \
        && rg -q 'Initialize engine version:' "${LOG_PATH}" \
        && rg -q 'UnloadTime:' "${LOG_PATH}"; then
        break
    fi

    if ! kill -0 "${player_pid}" 2>/dev/null; then
        break
    fi
    sleep 0.2
done

[[ -f "${LOG_PATH}" ]] || {
    printf 'Player smoke log was not created.\n' >&2
    exit 1
}
rg -q 'Initialize engine version: 6000\.5\.3f1' "${LOG_PATH}" || {
    printf 'Player did not initialize the locked Unity runtime.\n' >&2
    exit 1
}
rg -q 'UnloadTime:' "${LOG_PATH}" || {
    printf 'Player did not finish loading the boot scene.\n' >&2
    exit 1
}
if rg -q 'ConfigDatabase entered safe mode|NullReferenceException|Unhandled Exception|Crash!!!' \
    "${LOG_PATH}"; then
    printf 'Player smoke detected a blocking runtime error.\n' >&2
    exit 1
fi

printf '%s\n' \
    '{' \
    '  "result": "Passed",' \
    '  "unityVersion": "6000.5.3f1",' \
    '  "runtimeInitialized": true,' \
    '  "bootSceneLoaded": true,' \
    '  "configSafeMode": false,' \
    '  "blockingException": false' \
    '}' > "${REPORT_PATH}"

printf 'macOS player smoke passed: runtime initialized and boot scene loaded.\n'
