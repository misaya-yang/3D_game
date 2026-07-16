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
        printf 'Required contract not found in %s: %s\n'             "${file_path}" "${pattern}" >&2
        exit 1
    }
}

cd "${PROJECT_ROOT}"
"${SCRIPT_DIR}/check_docs_consistency.sh"
bash -n "${SCRIPT_DIR}/validate_g09_01.sh"

required_files=(
    Assets/_Project/Art/ThirdParty/UI_SHA256SUMS
    Assets/_Project/Art/ThirdParty/Licenses/Kenney_UI_Pack_RPG_Expansion_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Kenney_Game_Icons_CC0.txt
    Assets/_Project/Resources/UI/G09/Backgrounds/main_menu_misty_path_v1.png
    Assets/_Project/Scripts/Data/Save/GameSettingsData.cs
    Assets/_Project/Scripts/Systems/Feedback/GameSettingsRuntime.cs
    Assets/_Project/Scripts/Systems/UI/Common/GameplayMenuBarView.cs
    Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiFactory.cs
    Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiTheme.cs
    Assets/_Project/Tests/PlayMode/VerticalSlice/G0901PlayModeTests.cs
    docs/ui/G09-01_UI_AUDIT.md
    docs/ui/THIRD_PARTY_UI_ASSETS.md
    docs/ui/GENERATED_UI_ASSETS.md
)
for file_path in "${required_files[@]}"; do
    assert_file "${file_path}"
done

[[ "$(find Assets/_Project/Resources/UI/G09/Frames -type f -name '*.png' | wc -l | tr -d ' ')" == 11 ]] || {
    printf 'Expected exactly 11 curated UI frame assets.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Resources/UI/G09/Icons -type f -name '*.png' | wc -l | tr -d ' ')" == 23 ]] || {
    printf 'Expected exactly 23 curated UI icon assets.\n' >&2
    exit 1
}
[[ "$(find docs/ui/previews/before -type f -name '*.png' | wc -l | tr -d ' ')" -ge 9 ]] || {
    printf 'Expected at least 9 before screenshots.\n' >&2
    exit 1
}
[[ "$(find docs/ui/previews/after -type f -name '*.png' | wc -l | tr -d ' ')" -ge 19 ]] || {
    printf 'Expected at least 19 after screenshots.\n' >&2
    exit 1
}

shasum -a 256 -c Assets/_Project/Art/ThirdParty/UI_SHA256SUMS >/dev/null
printf '%s  %s\n'     '6a6295acfe03b3a3d94df849d8d379432d66d0ad4582bce102bf8a15c89994b6'     'Assets/_Project/Resources/UI/G09/Backgrounds/main_menu_misty_path_v1.png'     | shasum -a 256 -c - >/dev/null

assert_pattern 'actionsAsset\?\.Enable\(\)'     Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiFactory.cs
assert_pattern 'class GameplayMenuBarView'     Assets/_Project/Scripts/Systems/UI/Common/GameplayMenuBarView.cs
assert_pattern 'settings\.json'     Assets/_Project/Scripts/Data/Save/GameSettingsData.cs
assert_pattern 'gameManager\.State == GameState\.Playing'     Assets/_Project/Scripts/Systems/UI/Common/UIManager.cs
assert_pattern 'renderer\.enabled = false'     Assets/_Project/Scripts/Systems/World/WorldAreaMarker.cs
assert_pattern 'DialogueShutdownToleratesDestroyedPlayerInput'     Assets/_Project/Tests/PlayMode/VerticalSlice/G0901PlayModeTests.cs
if ! rg -U -q     'id: G09-01\nphase: 9\nstatus: (in_progress|implemented|done)'     docs/10_GOALS.md; then
    printf 'G09-01 must be active or delivered in docs/10_GOALS.md.\n' >&2
    exit 1
fi

printf 'G09-01 static validation passed.\n'
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}"     -batchmode -nographics -projectPath "${PROJECT_ROOT}"     -runTests -testPlatform PlayMode     -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0901PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.GVS05PlayModeTests;Wendao.Tests.PlayMode.SceneFlow.SceneLoaderPlayModeTests.MainMenuButtonLoadsQingshiThroughLoadingSceneAndPublishesMapInfo'     -testResults "${RESULTS_DIR}/G09-01-targeted-playmode.xml"     -logFile "${RESULTS_DIR}/G09-01-targeted-playmode.log"

"${UNITY_EDITOR}"     -batchmode -nographics -projectPath "${PROJECT_ROOT}"     -runTests -testPlatform EditMode     -testResults "${RESULTS_DIR}/G09-01-full-editmode.xml"     -logFile "${RESULTS_DIR}/G09-01-full-editmode.log"

"${UNITY_EDITOR}"     -batchmode -nographics -projectPath "${PROJECT_ROOT}"     -runTests -testPlatform PlayMode     -testResults "${RESULTS_DIR}/G09-01-full-playmode.xml"     -logFile "${RESULTS_DIR}/G09-01-full-playmode.log"

"${SCRIPT_DIR}/build_macos.sh"
"${SCRIPT_DIR}/smoke_macos_player.sh"

printf 'G09-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
