#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly BUILD_PATH="${WENDAO_BUILD_PATH:-${PROJECT_ROOT}/Builds/macOS/WendaoChangsheng.app}"
readonly REPORT_PATH="${PROJECT_ROOT}/TestResults/G07-04-build.json"
readonly LOG_PATH="${PROJECT_ROOT}/TestResults/G07-04-build.log"

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "$(dirname "${BUILD_PATH}")" "${PROJECT_ROOT}/TestResults"
rm -rf "${BUILD_PATH}"

"${UNITY_EDITOR}" \
    -batchmode -quit -projectPath "${PROJECT_ROOT}" \
    -executeMethod Wendao.Editor.WendaoBuild.BuildMacos \
    -wendaoBuildPath "${BUILD_PATH}" \
    -wendaoBuildReport "${REPORT_PATH}" \
    -logFile "${LOG_PATH}"

[[ -d "${BUILD_PATH}" ]] || {
    printf 'Build output missing: %s\n' "${BUILD_PATH}" >&2
    exit 1
}
rg -q '"result": "Succeeded"' "${REPORT_PATH}" || {
    printf 'Build report did not record success: %s\n' "${REPORT_PATH}" >&2
    exit 1
}

printf 'macOS release build passed: %s\n' "${BUILD_PATH}"
