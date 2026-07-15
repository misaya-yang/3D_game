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

assert_json_binding() {
    local action="$1"
    local path="$2"
    if ! jq -e --arg action "${action}" --arg path "${path}" \
        '.maps[] | .bindings[] | select(.action == $action and .path == $path)' \
        "${PROJECT_ROOT}/${input_actions}" >/dev/null; then
        printf 'Input action %s is missing binding %s.\n' "${action}" "${path}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g00_06.sh" --static-only

    local input_actions="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local camera="Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs"
    local prefab="Assets/_Project/Resources/Prefabs/Player/Player_Greybox.prefab"
    local tutorial_contract="Assets/_Project/Scripts/Systems/Tutorial/ITutorialService.cs"
    local tutorial="Assets/_Project/Scripts/Systems/Tutorial/TutorialManager.cs"
    local toast="Assets/_Project/Scripts/Systems/UI/Tutorial/TutorialToastView.cs"
    local ui_factory="Assets/_Project/Scripts/Systems/UI/SceneFlow/RuntimeUiFactory.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS01PlayModeTests.cs"
    local test_asmdef="Assets/_Project/Tests/PlayMode/VerticalSlice/Wendao.VerticalSlice.PlayModeTests.asmdef"

    local required_files=(
        "${input_actions}"
        "${input_contract}"
        "${input_reader}"
        "${input_reader}.meta"
        "${player}"
        "${player}.meta"
        "${player_bootstrap}"
        "${camera}"
        "${prefab}"
        "${prefab}.meta"
        "${tutorial_contract}"
        "${tutorial}"
        "${toast}"
        "${ui_factory}"
        "${tests}"
        "${test_asmdef}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    jq empty "${PROJECT_ROOT}/${input_actions}"
    jq empty "${PROJECT_ROOT}/${test_asmdef}"
    [[ "$(jq -r '.name' "${PROJECT_ROOT}/${input_actions}")" == "PlayerInputActions" ]]
    [[ "$(jq -r '.maps[0].name' "${PROJECT_ROOT}/${input_actions}")" == "Player" ]]

    local required_actions=(
        Move Look Jump Sprint LightAttack HeavyAttack Dodge Block LockOn
        Skill1 Skill2 Skill3 Skill4 Interact OpenInventory OpenCharacter
        OpenSkill OpenMap OpenQuest Pause Mount
    )
    local action
    for action in "${required_actions[@]}"; do
        if ! jq -e --arg action "${action}" \
            '.maps[0].actions[] | select(.name == $action)' \
            "${PROJECT_ROOT}/${input_actions}" >/dev/null; then
            printf 'Input action is missing: %s\n' "${action}" >&2
            exit 1
        fi
    done

    assert_json_binding Move '<Keyboard>/w'
    assert_json_binding Move '<Gamepad>/leftStick'
    assert_json_binding Look '<Mouse>/delta'
    assert_json_binding Look '<Gamepad>/rightStick'
    assert_json_binding Jump '<Keyboard>/space'
    assert_json_binding Jump '<Gamepad>/buttonSouth'
    assert_json_binding Sprint '<Keyboard>/leftShift'
    assert_json_binding Sprint '<Gamepad>/leftStickPress'

    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Scripts/Entities/Wendao.Entities.asmdef")" == '["Wendao.Core","Wendao.Systems","Wendao.Data","Wendao.Camera","Unity.InputSystem"]' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Scripts/Camera/Wendao.Camera.asmdef")" == '["Wendao.Core","Wendao.Data","Wendao.Systems"]' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/${test_asmdef}")" == '["Wendao.Core","Wendao.Data","Wendao.Systems","Wendao.Entities","Wendao.Camera","Wendao.UI","Unity.InputSystem","UnityEngine.UI"]' ]]

    assert_pattern 'public interface IPlayerInputSource' "${input_contract}"
    assert_pattern 'public Vector2 Move' "${input_reader}"
    assert_pattern 'FindActionMap\(PlayerActionMapName, true\)' "${input_reader}"
    assert_pattern 'ServiceLocator\.Register<IPlayerInputSource>' "${input_reader}"

    assert_pattern 'public sealed class PlayerController : SafeBehaviour' "${player}"
    assert_pattern '_walkSpeed = 5f' "${player}"
    assert_pattern '_sprintSpeed = 8f' "${player}"
    assert_pattern '_acceleration = 40f' "${player}"
    assert_pattern '_rotationSpeed = 720f' "${player}"
    assert_pattern '_jumpSpeed = 7f' "${player}"
    assert_pattern '_gravity = -20f' "${player}"
    assert_pattern 'PlayerState\.Jump' "${player}"
    assert_pattern 'PlayerState\.Fall' "${player}"
    assert_pattern 'public void SetInputEnabled\(bool enabled\)' "${player}"
    assert_pattern 'public void ForceState\(PlayerState state\)' "${player}"
    assert_pattern 'public void TeleportTo\(Vector3 position, Quaternion rotation\)' "${player}"

    assert_pattern 'public sealed class ThirdPersonCamera : MonoBehaviour' "${camera}"
    assert_pattern 'public void SetTarget\(Transform player\)' "${camera}"
    assert_pattern 'Physics\.SphereCastNonAlloc' "${camera}"
    assert_pattern 'public void SetLockOnTarget\(Transform target\)' "${camera}"
    assert_pattern 'public void SetDialogueFocus\(Transform npcFace, bool enable\)' "${camera}"

    assert_pattern 'PlayerPrefabResourcePath = "Prefabs/Player/Player_Greybox"' "${player_bootstrap}"
    assert_pattern 'QingshiGreyboxFactory\.EnsureCreated\(scene\)' "${player_bootstrap}"
    assert_pattern 'thirdPersonCamera\.SetTarget\(target\)' "${player_bootstrap}"
    assert_pattern 'm_Script: \{fileID: 11500000, guid: 27f3f64bd9514f33b9da670f47d30110, type: 3\}' "${prefab}"
    assert_pattern 'm_Script: \{fileID: 11500000, guid: 5ad75dddc0634b95ad15f7820b2de4c1, type: 3\}' "${prefab}"
    assert_pattern '^guid: 27f3f64bd9514f33b9da670f47d30110$' "${input_reader}.meta"
    assert_pattern '^guid: 5ad75dddc0634b95ad15f7820b2de4c1$' "${player}.meta"
    assert_pattern 'CharacterController:' "${prefab}"

    assert_pattern 'public interface ITutorialService' "${tutorial_contract}"
    assert_pattern 'public sealed class TutorialManager : SafeBehaviour, ITutorialService' "${tutorial}"
    assert_pattern 'MoveTutorialId = "tut_move"' "${tutorial}"
    assert_pattern 'TutorialPromptedEvent = "OnTutorialPrompted"' "${tutorial}"
    assert_pattern 'TutorialsCompleted' "${tutorial}"
    assert_pattern 'TrySaveModule\(WorldSaveModule\)' "${tutorial}"
    assert_pattern 'public bool HasCompleted\(string tutorialId\)' "${tutorial}"
    assert_pattern 'public sealed class TutorialToastView : MonoBehaviour' "${toast}"
    assert_pattern 'CurrentLocalizationKey' "${toast}"
    assert_pattern 'TutorialManager\.TutorialPromptedEvent' "${toast}"
    assert_pattern 'Font\.CreateDynamicFontFromOSFont' "${ui_factory}"
    assert_pattern 'Noto Sans CJK SC' "${ui_factory}"

    assert_pattern '^\| OnTutorialPrompted \| TutorialPromptInfo \| TutorialManager \| Tutorial Toast UI \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public struct TutorialPromptInfo' docs/03_DATA_LAYER.md
    assert_pattern 'public struct TutorialPromptInfo' Assets/_Project/Scripts/Data/Runtime/EventParams.cs
    assert_pattern '^\| tutorial_move_move \| 使用 WASD 或左摇杆移动。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_move_look \| 移动鼠标或右摇杆转动视角。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_move_jump \| 按空格键或手柄南键跳跃。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_move_complete \| 基础身法已掌握。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tut_move \| 首次进入青石 \|$' docs/09_CONTENT.md

    assert_pattern 'EnsureDefaultSaveReady\(\)' Assets/_Project/Scripts/Systems/UI/SceneFlow/MainMenuView.cs
    assert_pattern 'saveManager\.LoadGame\(defaultSlot\)' Assets/_Project/Scripts/Systems/UI/SceneFlow/MainMenuView.cs
    assert_pattern 'saveManager\.SaveGame\(defaultSlot\)' Assets/_Project/Scripts/Systems/UI/SceneFlow/MainMenuView.cs

    assert_pattern 'InputActionsContainKeyboardMouseAndGamepadLocomotionBindings' "${tests}"
    assert_pattern 'PlayerMovesSprintsJumpsAndCameraAvoidsObstacle' "${tests}"
    assert_pattern 'MoveTutorialCompletesPersistsAndDoesNotReplayAfterLoad' "${tests}"
    assert_pattern 'reloadedSave\.LoadGame\(0\)' "${tests}"

    printf 'G-VS-01 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testResults "${RESULTS_DIR}/GVS-01-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-01-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode \
    -testResults "${RESULTS_DIR}/GVS-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-01-playmode.log"

printf 'G-VS-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
