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
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    fi
}

assert_json_binding() {
    local action="$1"
    local path="$2"
    local input_actions="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    if ! jq -e --arg action "${action}" --arg path "${path}" \
        '.maps[] | .bindings[] | select(.action == $action and .path == $path)' \
        "${PROJECT_ROOT}/${input_actions}" >/dev/null; then
        printf 'Input action %s is missing binding %s.\n' "${action}" "${path}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_gvs_02.sh" --static-only

    local runtime_data="Assets/_Project/Scripts/Data/Runtime/InventoryRuntimeData.cs"
    local event_params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local inventory_contract="Assets/_Project/Scripts/Systems/Inventory/IInventoryService.cs"
    local inventory="Assets/_Project/Scripts/Systems/Inventory/InventoryManager.cs"
    local item_use="Assets/_Project/Scripts/Systems/Inventory/ItemUseSystem.cs"
    local equipment_contract="Assets/_Project/Scripts/Systems/Equipment/IEquipmentService.cs"
    local equipment="Assets/_Project/Scripts/Systems/Equipment/EquipmentManager.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local inventory_ui="Assets/_Project/Scripts/Systems/UI/Inventory/InventoryPanelView.cs"
    local toast_ui="Assets/_Project/Scripts/Systems/UI/Common/GameToastView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS03PlayModeTests.cs"

    local required_files=(
        "${runtime_data}"
        "${event_params}"
        "${config}"
        "${inventory_contract}"
        "Assets/_Project/Scripts/Systems/Inventory/IItemUseService.cs"
        "Assets/_Project/Scripts/Systems/Inventory/IPlayerHealthService.cs"
        "Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
        "${inventory}"
        "${item_use}"
        "${equipment_contract}"
        "${equipment}"
        "${player_stats}"
        "${input_contract}"
        "${input_reader}"
        "${bootstrap}"
        "${inventory_ui}"
        "${toast_ui}"
        "${ui_bootstrap}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'public sealed class InventorySlot' "${runtime_data}"
    assert_pattern 'public sealed class EquipmentInstance' "${runtime_data}"
    assert_pattern 'public sealed class InventorySaveData' "${runtime_data}"
    assert_pattern 'public sealed class EquipmentSaveData' "${runtime_data}"
    assert_pattern 'public struct ToastInfo' "${event_params}"

    assert_pattern 'GetEquipment\(string id\)' "${config}"
    assert_pattern 'item_potion_heal_01' "${config}"
    assert_pattern 'eq_weapon_wood_sword' "${config}"
    assert_pattern 'Value = 80f' "${config}"
    assert_pattern 'Attack = 8f' "${config}"
    assert_pattern 'MaxStack = 20' "${config}"

    assert_pattern 'public interface IInventoryService' "${inventory_contract}"
    assert_pattern 'public sealed class InventoryManager : SafeBehaviour, IInventoryService' "${inventory}"
    assert_pattern 'public const int Capacity = 50' "${inventory}"
    assert_pattern 'SaveModuleName = "inventory"' "${inventory}"
    assert_pattern 'ServiceLocator\.Register<IInventoryService>' "${inventory}"
    assert_pattern 'RegisterModule\(' "${inventory}"
    assert_pattern 'InventoryEvents\.ItemAcquired' "${inventory}"
    assert_pattern 'public bool AddItem\(' "${inventory}"
    assert_pattern 'public bool RemoveAt\(' "${inventory}"
    assert_pattern 'public EquipmentInstance CreateEquipmentInstance' "${inventory}"
    assert_pattern 'Slots = _slots\.Select\(CloneSlot\)' "${inventory}"

    assert_pattern 'public sealed class ItemUseSystem : SafeBehaviour, IItemUseService' "${item_use}"
    assert_pattern 'FullHpToastKey = "ui_item_use_full_hp"' "${item_use}"
    assert_pattern 'inventory\.RemoveAt\(slotIndex, 1\)' "${item_use}"
    assert_pattern 'playerHealth\.ApplyHeal\(effect\.Value, item\.Id\)' "${item_use}"
    assert_pattern 'InventoryEvents\.ItemUsed' "${item_use}"

    assert_pattern 'public interface IEquipmentService' "${equipment_contract}"
    assert_pattern 'public sealed class EquipmentManager : SafeBehaviour, IEquipmentService' "${equipment}"
    assert_pattern 'SaveModuleName = "equipment"' "${equipment}"
    assert_pattern 'equipment\.Slot != EquipmentSlot\.Weapon' "${equipment}"
    assert_pattern 'TryTakeEquipmentAt' "${equipment}"
    assert_pattern 'TryStoreEquipment' "${equipment}"
    assert_pattern 'InventoryEvents\.EquipmentChanged' "${equipment}"
    assert_pattern 'public StatBlock GetEquipmentStats\(\)' "${equipment}"

    assert_pattern 'IPlayerHealthService' "${player_stats}"
    assert_pattern 'public float Attack =>.*Final' "${player_stats}"
    assert_pattern 'InventoryEvents\.EquipmentChanged' "${player_stats}"
    assert_pattern 'GetEquipmentStats\(\)' "${player_stats}"

    assert_json_binding OpenInventory '<Keyboard>/b'
    assert_json_binding OpenInventory '<Gamepad>/selectButton'
    assert_pattern 'bool OpenInventoryPressedThisFrame' "${input_contract}"
    assert_pattern '_openInventoryAction = _playerMap\.FindAction\("OpenInventory", true\)' "${input_reader}"
    assert_pattern 'CanReadUi\(_openInventoryAction\)' "${input_reader}"

    assert_pattern 'AddComponent<InventoryManager>' "${bootstrap}"
    assert_pattern 'AddComponent<ItemUseSystem>' "${bootstrap}"
    assert_pattern 'AddComponent<EquipmentManager>' "${bootstrap}"
    assert_pattern 'public sealed class InventoryPanelView : MonoBehaviour' "${inventory_ui}"
    assert_pattern 'new Button\[InventoryManager\.Capacity\]' "${inventory_ui}"
    assert_pattern 'OpenInventoryPressedThisFrame' "${inventory_ui}"
    assert_pattern 'item_name_' "${inventory_ui}"
    assert_pattern 'public sealed class GameToastView : MonoBehaviour' "${toast_ui}"
    assert_pattern 'UiEvents\.ToastRequested' "${toast_ui}"
    assert_pattern 'AddComponent<InventoryPanelView>' "${ui_bootstrap}"
    assert_pattern 'AddComponent<GameToastView>' "${ui_bootstrap}"

    if rg -q 'class RefineSystem|class Shop|Vendor|TryRefine' \
        "${PROJECT_ROOT}/${inventory}" \
        "${PROJECT_ROOT}/${item_use}" \
        "${PROJECT_ROOT}/${equipment}" \
        "${PROJECT_ROOT}/${inventory_ui}"; then
        printf 'G-VS-03 contains out-of-scope refine or shop implementation.\n' >&2
        exit 1
    fi

    assert_pattern '^\| OnToastRequested \| ToastInfo \| ItemUse/Equipment/各系统 \| Top Toast UI \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public sealed class InventorySaveData' docs/03_DATA_LAYER.md
    assert_pattern 'public sealed class EquipmentSaveData' docs/03_DATA_LAYER.md
    assert_pattern '^\| item_potion_heal_01 \| 回血丹 \| Heal 80 \| 20 \|$' docs/09_CONTENT.md
    assert_pattern '^\| eq_weapon_wood_sword \| Weapon \| 练气 \| Atk\+8 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_inventory_title \| 背包 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_item_use_full_hp \| 气血已满，无需服丹。 \|$' docs/09_CONTENT.md

    assert_pattern 'BuiltInContentAndInventoryInputMatchAuthoritativeIdsAndValues' "${tests}"
    assert_pattern 'HealPotionRestoresHpPublishesEventsAndIsNotConsumedAtFullHp' "${tests}"
    assert_pattern 'EquippingWoodSwordRaisesAttackAndChangesComputedDamage' "${tests}"
    assert_pattern 'InventoryEquipmentAndCurrencyRoundTripThroughSeparateSaveModules' "${tests}"
    assert_pattern 'MinimalInventoryUiHasFiftySlotsAndSuspendsGameplayInput' "${tests}"

    printf 'G-VS-03 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testResults "${RESULTS_DIR}/GVS-03-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-03-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode \
    -testResults "${RESULTS_DIR}/GVS-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-03-playmode.log"

printf 'G-VS-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
