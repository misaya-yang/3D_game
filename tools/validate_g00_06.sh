#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"
readonly LOADING_GUID="d4e6c8a0b2f1497aa1c3e5f708192b4d"
readonly QINGSHI_GUID="e5f7a9b1c3d2486fb0a2c4e618395a7b"

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

assert_meta_guid() {
    local file_path="$1"
    local expected="$2"
    local actual
    actual="$(sed -n 's/^guid: //p' "${PROJECT_ROOT}/${file_path}")"
    if [[ "${actual}" != "${expected}" ]]; then
        printf 'Unexpected guid in %s: expected %s, got %s\n' \
            "${file_path}" "${expected}" "${actual}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g00_05.sh" --static-only

    local scene_loader="Assets/_Project/Scripts/Systems/World/SceneLoader.cs"
    local progress="Assets/_Project/Scripts/Systems/World/SceneLoadProgress.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local greybox="Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    local main_menu="Assets/_Project/Scripts/Systems/UI/SceneFlow/MainMenuView.cs"
    local loading_view="Assets/_Project/Scripts/Systems/UI/SceneFlow/LoadingView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local ui_factory="Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiFactory.cs"
    local edit_tests="Assets/_Project/Tests/EditMode/Systems/SceneLoadProgressTests.cs"
    local edit_asmdef="Assets/_Project/Tests/EditMode/Systems/Wendao.Systems.EditModeTests.asmdef"
    local play_tests="Assets/_Project/Tests/PlayMode/SceneFlow/SceneLoaderPlayModeTests.cs"
    local play_asmdef="Assets/_Project/Tests/PlayMode/SceneFlow/Wendao.SceneFlow.PlayModeTests.asmdef"

    local required_files=(
        "${scene_loader}"
        "${progress}"
        "${bootstrap}"
        "${greybox}"
        "${main_menu}"
        "${loading_view}"
        "${ui_bootstrap}"
        "${ui_factory}"
        "${edit_tests}"
        "${edit_asmdef}"
        "${play_tests}"
        "${play_asmdef}"
        Assets/_Project/Scenes/Core/Loading.unity
        Assets/_Project/Scenes/Core/Loading.unity.meta
        Assets/_Project/Scenes/Maps/Map_Qingshi.unity
        Assets/_Project/Scenes/Maps/Map_Qingshi.unity.meta
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    jq empty "${PROJECT_ROOT}/${edit_asmdef}"
    jq empty "${PROJECT_ROOT}/${play_asmdef}"
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/${edit_asmdef}")" == '["Wendao.Core","Wendao.Data","Wendao.Systems"]' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/${play_asmdef}")" == '["Wendao.Core","Wendao.Data","Wendao.Systems","Wendao.UI","Unity.InputSystem","UnityEngine.UI"]' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Scripts/Systems/UI/Wendao.UI.asmdef")" == '["Wendao.Systems","Wendao.Data","Unity.InputSystem","UnityEngine.UI"]' ]]

    assert_meta_guid Assets/_Project/Scenes/Core/Loading.unity.meta "${LOADING_GUID}"
    assert_meta_guid Assets/_Project/Scenes/Maps/Map_Qingshi.unity.meta "${QINGSHI_GUID}"
    assert_pattern '^%YAML 1\.1$' Assets/_Project/Scenes/Core/Loading.unity
    assert_pattern '^%YAML 1\.1$' Assets/_Project/Scenes/Maps/Map_Qingshi.unity
    assert_pattern 'path: Assets/_Project/Scenes/Core/Loading\.unity' ProjectSettings/EditorBuildSettings.asset
    assert_pattern "guid: ${LOADING_GUID}" ProjectSettings/EditorBuildSettings.asset
    assert_pattern 'path: Assets/_Project/Scenes/Maps/Map_Qingshi\.unity' ProjectSettings/EditorBuildSettings.asset
    assert_pattern "guid: ${QINGSHI_GUID}" ProjectSettings/EditorBuildSettings.asset

    assert_pattern 'public sealed class SceneLoader : Singleton<SceneLoader>' "${scene_loader}"
    assert_pattern 'public bool LoadMap\(string mapId, string spawnId\)' "${scene_loader}"
    assert_pattern 'SceneManager\.LoadSceneAsync' "${scene_loader}"
    assert_pattern 'targetOperation\.allowSceneActivation = false' "${scene_loader}"
    assert_pattern 'targetOperation\.allowSceneActivation = true' "${scene_loader}"
    assert_pattern 'public event Action<float> ProgressChanged' "${scene_loader}"
    assert_pattern 'public const string MapLoadedEvent = "OnMapLoaded"' "${scene_loader}"
    assert_pattern 'EventBus\.Publish\(' "${scene_loader}"
    assert_pattern 'new MapInfo' "${scene_loader}"
    assert_pattern 'GameState\.Playing' "${scene_loader}"
    assert_pattern 'DefaultMapId = "map_qingshi"' "${scene_loader}"
    assert_pattern 'DefaultMapSceneName = "Map_Qingshi"' "${scene_loader}"

    assert_pattern 'public sealed class SceneLoadProgress' "${progress}"
    assert_pattern 'if \(next <= Value\)' "${progress}"
    assert_pattern 'Value = next;' "${progress}"
    assert_pattern 'progress\.Report\(1f\)' "${edit_tests}"
    assert_pattern 'ReportsAreMonotonicClampedAndReachExactlyOne' "${edit_tests}"

    assert_pattern 'GameState\.Loading,' Assets/_Project/Scripts/Core/GameManager.cs
    assert_pattern 'PlayingCanEnterLoadingForMapTravel' Assets/_Project/Tests/PlayMode/GameManagerPlayModeTests.cs
    assert_pattern 'EnsureSaveManager\(\)' "${bootstrap}"
    assert_pattern 'EnsureConfigDatabase\(\)' "${bootstrap}"
    assert_pattern 'loader\.LoadMainMenu\(\)' "${bootstrap}"
    assert_pattern 'QingshiGreyboxFactory\.EnsureCreated\(scene\)' "${bootstrap}"

    assert_pattern 'TitleLocalizationKey = "ui_main_menu_title"' "${main_menu}"
    assert_pattern 'StartGameLocalizationKey = "ui_main_menu_start_game"' "${main_menu}"
    assert_pattern 'StartButton\.onClick\.AddListener' "${main_menu}"
    assert_pattern 'loader\.LoadMap\(SceneLoader\.DefaultMapId, string\.Empty\)' "${main_menu}"
    assert_pattern 'LoadingLocalizationKey = "ui_loading_entering_world"' "${loading_view}"
    assert_pattern 'ProgressLocalizationKey = "ui_loading_progress_percent"' "${loading_view}"
    assert_pattern 'ProgressDefaultFormat = "进度 \{0\}%"' "${loading_view}"
    assert_pattern 'text\.text = value \?\? string\.Empty' Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiFactory.cs
    assert_pattern 'ProgressFill\.fillAmount = DisplayedProgress' "${loading_view}"
    assert_pattern 'typeof\(InputSystemUIInputModule\)' "${ui_factory}"
    assert_pattern '^\| ui_main_menu_title \| 问道长生 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_main_menu_start_game \| 踏入仙途 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_loading_entering_world \| 正在步入仙途… \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_loading_progress_percent \| 进度 \{0\}% \|$' docs/09_CONTENT.md

    assert_pattern 'MainMenuButtonLoadsQingshiThroughLoadingSceneAndPublishesMapInfo' "${play_tests}"
    assert_pattern 'loadingViewObserved' "${play_tests}"
    assert_pattern 'observedMap\.MapId' "${play_tests}"
    assert_pattern 'QingshiGreyboxFactory\.GroundName' "${play_tests}"

    if rg -q 'Enemy|NPC|Quest|Loot|Dungeon' "${PROJECT_ROOT}/${greybox}"; then
        printf 'G00-06 greybox contains map gameplay content, which is out of scope.\n' >&2
        exit 1
    fi
    if rg -q 'Gather' "${PROJECT_ROOT}/${greybox}" \
        && [[ ! -f "${PROJECT_ROOT}/Assets/_Project/Scripts/Systems/Crafting/GatheringSystem.cs" ]]; then
        printf 'Qingshi gathering content exists without its owning G03-04 system.\n' >&2
        exit 1
    fi

    printf 'G00-06 static validation passed. Unity compilation and scene-flow tests remain pending.\n'
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
    -testResults "${RESULTS_DIR}/G00-06-editmode.xml" \
    -logFile "${RESULTS_DIR}/G00-06-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode \
    -testResults "${RESULTS_DIR}/G00-06-playmode.xml" \
    -logFile "${RESULTS_DIR}/G00-06-playmode.log"

printf 'G00-06 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
