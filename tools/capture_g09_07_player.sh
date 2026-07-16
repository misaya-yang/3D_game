#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly APP_PATH="${WENDAO_BUILD_PATH:-${PROJECT_ROOT}/Builds/macOS/WendaoChangsheng.app}"
readonly EXECUTABLE="${APP_PATH}/Contents/MacOS/问道长生"
readonly CAPTURE_PATH="${PROJECT_ROOT}/docs/art/previews/g09-07/final/02-full-character-lineup-1920x1080.png"
readonly LOG_PATH="${PROJECT_ROOT}/TestResults/G09-07-player-lineup.log"
readonly REPORT_PATH="${PROJECT_ROOT}/TestResults/G09-07-player-lineup.json"

[[ -x "${EXECUTABLE}" ]] || {
    printf 'Built player executable missing: %s\n' "${EXECUTABLE}" >&2
    exit 2
}

if pgrep -f "${EXECUTABLE}" >/dev/null 2>&1; then
    printf 'A Wendao Player is already running; refusing to open another.\n' >&2
    exit 3
fi

mkdir -p \
    "$(dirname "${CAPTURE_PATH}")" \
    "${PROJECT_ROOT}/TestResults"
find "$(dirname "${CAPTURE_PATH}")" \
    -maxdepth 1 \
    -type f \
    -name "$(basename "${CAPTURE_PATH}")" \
    -delete

"${EXECUTABLE}" \
    -screen-fullscreen 0 \
    -screen-width 1920 \
    -screen-height 1080 \
    -wendaoShowcaseScene Map_Qingshi \
    -wendaoShowcaseArtView final_character_lineup \
    -wendaoHideUi \
    -wendaoCaptureWidth 1920 \
    -wendaoCaptureHeight 1080 \
    -wendaoCapturePath "${CAPTURE_PATH}" \
    -wendaoExitAfterCapture \
    -logFile "${LOG_PATH}" \
    >/dev/null 2>&1 &
player_pid=$!

cleanup() {
    if kill -0 "${player_pid}" 2>/dev/null; then
        kill "${player_pid}" 2>/dev/null || true
        wait "${player_pid}" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

deadline=$((SECONDS + 30))
while kill -0 "${player_pid}" 2>/dev/null; do
    if (( SECONDS >= deadline )); then
        printf 'G09-07 Player capture timed out.\n' >&2
        exit 1
    fi
    sleep 0.25
done
wait "${player_pid}"
trap - EXIT INT TERM

[[ -s "${CAPTURE_PATH}" ]] || {
    printf 'G09-07 Player capture was not created.\n' >&2
    exit 1
}
rg -q 'G09-02 Player screenshot requested:' "${LOG_PATH}" || {
    printf 'Player did not confirm the screenshot request.\n' >&2
    exit 1
}
if rg -q \
    'NullReferenceException|Unhandled Exception|Crash!!!|InternalErrorShader' \
    "${LOG_PATH}"; then
    printf 'G09-07 Player capture detected a blocking error.\n' >&2
    exit 1
fi

capture_sha="$(shasum -a 256 "${CAPTURE_PATH}" | awk '{print $1}')"
capture_bytes="$(stat -f '%z' "${CAPTURE_PATH}")"
printf '%s\n' \
    '{' \
    '  "result": "Passed",' \
    '  "unityVersion": "6000.5.3f1",' \
    '  "windowCount": 1,' \
    '  "autoExited": true,' \
    '  "view": "final_character_lineup",' \
    "  \"captureBytes\": ${capture_bytes}," \
    "  \"captureSha256\": \"${capture_sha}\"" \
    '}' > "${REPORT_PATH}"

printf 'G09-07 single-window Player capture passed: %s\n' \
    "${CAPTURE_PATH}"
