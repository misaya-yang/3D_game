#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"
readonly PIPELINE_GUID="77a76618f20dd9947aaaf059007f07d6"
readonly RENDERER_GUID="2064643b54e611442a526a0a50f9430a"
readonly GLOBAL_SETTINGS_GUID="feff265643db0b441acef4669327eebc"

STATIC_ONLY=false

case "${1:-}" in
    "") ;;
    --static-only) STATIC_ONLY=true ;;
    *)
        printf 'Usage: %s [--static-only]\n' "$0" >&2
        exit 64
        ;;
esac

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        printf 'Required command not found: %s\n' "$1" >&2
        exit 69
    fi
}

assert_file() {
    if [[ ! -f "${PROJECT_ROOT}/$1" ]]; then
        printf 'Required file missing: %s\n' "$1" >&2
        exit 1
    fi
}

assert_directory() {
    if [[ ! -d "${PROJECT_ROOT}/$1" ]]; then
        printf 'Required directory missing: %s\n' "$1" >&2
        exit 1
    fi
}

assert_references() {
    local file_path="$1"
    local expected="$2"
    local actual
    actual="$(jq -c '.references' "${PROJECT_ROOT}/${file_path}")"
    if [[ "${actual}" != "${expected}" ]]; then
        printf 'Unexpected asmdef references in %s: expected %s, got %s\n' \
            "${file_path}" "${expected}" "${actual}" >&2
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
    require_command jq
    require_command rg

    local required_directories=(
        Assets/_Project/Scripts/Core
        Assets/_Project/Scripts/Data/ScriptableObjects
        Assets/_Project/Scripts/Data/Runtime
        Assets/_Project/Scripts/Data/Config
        Assets/_Project/Scripts/Data/Enums
        Assets/_Project/Scripts/Systems/Cultivation
        Assets/_Project/Scripts/Systems/Combat
        Assets/_Project/Scripts/Systems/Inventory
        Assets/_Project/Scripts/Systems/Equipment
        Assets/_Project/Scripts/Systems/Skill
        Assets/_Project/Scripts/Systems/Crafting
        Assets/_Project/Scripts/Systems/Quest
        Assets/_Project/Scripts/Systems/NPC
        Assets/_Project/Scripts/Systems/Enemy
        Assets/_Project/Scripts/Systems/Mount
        Assets/_Project/Scripts/Systems/Faction
        Assets/_Project/Scripts/Systems/Achievement
        Assets/_Project/Scripts/Systems/World
        Assets/_Project/Scripts/Systems/UI
        Assets/_Project/Scripts/Entities
        Assets/_Project/Scripts/Camera
        Assets/_Project/Scripts/Audio
        Assets/_Project/Scripts/VFX
        Assets/_Project/Scripts/Network
        Assets/_Project/Scripts/Utils
        Assets/_Project/Prefabs
        Assets/_Project/ScriptableObjects
        Assets/_Project/Scenes/Core
        Assets/_Project/Scenes/Maps
        Assets/_Project/Scenes/Dungeons
        Assets/_Project/UI
        Assets/_Project/Art
        Assets/_Project/Audio
        Assets/_Project/Animations
        Assets/_Project/Addressables
        Assets/StreamingAssets/Config
        Assets/_Project/Settings
    )

    local directory
    for directory in "${required_directories[@]}"; do
        assert_directory "${directory}"
    done

    local runtime_asmdefs=(
        Assets/_Project/Scripts/Core/Wendao.Core.asmdef
        Assets/_Project/Scripts/Data/Wendao.Data.asmdef
        Assets/_Project/Scripts/Systems/Wendao.Systems.asmdef
        Assets/_Project/Scripts/Entities/Wendao.Entities.asmdef
        Assets/_Project/Scripts/Systems/UI/Wendao.UI.asmdef
        Assets/_Project/Scripts/Camera/Wendao.Camera.asmdef
    )

    local asmdef
    for asmdef in "${runtime_asmdefs[@]}"; do
        assert_file "${asmdef}"
        jq empty "${PROJECT_ROOT}/${asmdef}"
    done

    assert_references "${runtime_asmdefs[0]}" '[]'
    assert_references "${runtime_asmdefs[1]}" '["Wendao.Core","Unity.Newtonsoft.Json"]'
    assert_references "${runtime_asmdefs[2]}" '["Wendao.Core","Wendao.Data"]'
    assert_references "${runtime_asmdefs[3]}" '["Wendao.Core","Wendao.Systems","Wendao.Data","Wendao.Camera","Unity.InputSystem"]'
    assert_references "${runtime_asmdefs[4]}" '["Wendao.Core","Wendao.Systems","Wendao.Data","Wendao.Entities","Unity.InputSystem","UnityEngine.UI"]'
    assert_references "${runtime_asmdefs[5]}" '["Wendao.Core","Wendao.Data","Wendao.Systems"]'

    assert_file Packages/manifest.json
    jq empty "${PROJECT_ROOT}/Packages/manifest.json"
    [[ "$(jq -r '.dependencies["com.unity.render-pipelines.universal"]' "${PROJECT_ROOT}/Packages/manifest.json")" == "17.6.0" ]]
    [[ "$(jq -r '.dependencies["com.unity.inputsystem"]' "${PROJECT_ROOT}/Packages/manifest.json")" == "1.19.0" ]]
    rg -q '^m_EditorVersion: 6000\.5\.3f1$' "${PROJECT_ROOT}/ProjectSettings/ProjectVersion.txt"

    assert_file Assets/_Project/Scenes/Core/Boot.unity
    assert_file Assets/_Project/Scenes/Core/MainMenu.unity
    assert_meta_guid Assets/_Project/Scenes/Core/Boot.unity.meta a18db0b66e9f4b5da0c8f10cc441dace
    assert_meta_guid Assets/_Project/Scenes/Core/MainMenu.unity.meta c2d2fda5d3c845b6b28ea99d1b6378fe
    rg -q 'path: Assets/_Project/Scenes/Core/Boot\.unity' "${PROJECT_ROOT}/ProjectSettings/EditorBuildSettings.asset"
    rg -q 'path: Assets/_Project/Scenes/Core/MainMenu\.unity' "${PROJECT_ROOT}/ProjectSettings/EditorBuildSettings.asset"

    assert_meta_guid Assets/_Project/Settings/Wendao_UniversalRenderPipeline.asset.meta "${PIPELINE_GUID}"
    assert_meta_guid Assets/_Project/Settings/Wendao_UniversalRenderer.asset.meta "${RENDERER_GUID}"
    assert_meta_guid Assets/_Project/Settings/Wendao_UniversalRenderPipelineGlobalSettings.asset.meta "${GLOBAL_SETTINGS_GUID}"
    rg -q "guid: ${RENDERER_GUID}" "${PROJECT_ROOT}/Assets/_Project/Settings/Wendao_UniversalRenderPipeline.asset"
    rg -q "guid: ${PIPELINE_GUID}" "${PROJECT_ROOT}/ProjectSettings/GraphicsSettings.asset"
    rg -q "guid: ${GLOBAL_SETTINGS_GUID}" "${PROJECT_ROOT}/ProjectSettings/GraphicsSettings.asset"
    if rg 'customRenderPipeline:' "${PROJECT_ROOT}/ProjectSettings/QualitySettings.asset" | rg -qv "guid: ${PIPELINE_GUID}"; then
        printf 'QualitySettings contains a render pipeline other than the Wendao URP asset.\n' >&2
        exit 1
    fi
    if rg -q 'ARBackground|ARCommandBuffer' "${PROJECT_ROOT}/Assets/_Project/Settings/Wendao_UniversalRenderer.asset"; then
        printf 'Wendao renderer unexpectedly contains AR-only renderer features.\n' >&2
        exit 1
    fi

    printf 'G00-01 static validation passed.\n'
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
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.BootScenePlayModeTests \
    -testResults "${RESULTS_DIR}/G00-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G00-01-unity.log"

printf 'G00-01 Unity validation passed. Results: %s\n' "${RESULTS_DIR}/G00-01-playmode.xml"
