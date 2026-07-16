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

cd "${PROJECT_ROOT}"
"${SCRIPT_DIR}/check_docs_consistency.sh"
bash -n "${SCRIPT_DIR}/art/sync_budget_assets.sh"

[[ "$(find Assets/_Project/Resources/Art/Budget -type f -name '*.fbx' | wc -l | tr -d ' ')" -ge 58 ]] || {
    printf 'Expected at least 58 curated FBX models.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Resources/Art/Budget/Characters -type f -name '*.png' | wc -l | tr -d ' ')" -ge 5 ]] || {
    printf 'Expected at least 5 curated character textures.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Art/ThirdParty/Licenses -type f -name '*.txt' | wc -l | tr -d ' ')" -ge 3 ]] || {
    printf 'Expected at least 3 upstream license files.\n' >&2
    exit 1
}
shasum -a 256 -c Assets/_Project/Art/ThirdParty/SHA256SUMS >/dev/null
rg -q 'class BudgetVisualFactory' \
    Assets/_Project/Scripts/Entities/Visuals/BudgetVisualFactory.cs
rg -q 'class BudgetWorldArtBootstrap' \
    Assets/_Project/Scripts/Entities/Visuals/BudgetWorldArtBootstrap.cs
rg -q 'class G0803PlayModeTests' \
    Assets/_Project/Tests/PlayMode/VerticalSlice/G0803PlayModeTests.cs

printf 'G08-03 static validation passed.\n'
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testResults "${RESULTS_DIR}/G08-03-editmode.xml" \
    -logFile "${RESULTS_DIR}/G08-03-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testResults "${RESULTS_DIR}/G08-03-full-playmode.xml" \
    -logFile "${RESULTS_DIR}/G08-03-full-playmode.log"

"${SCRIPT_DIR}/build_macos.sh"
"${SCRIPT_DIR}/smoke_macos_player.sh"

printf 'G08-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
