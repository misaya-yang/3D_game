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

run_static_validation() {
    "${SCRIPT_DIR}/validate_g03_04.sh" --static-only

    local contract="Assets/_Project/Scripts/Systems/Shop/IShopService.cs"
    local ids="Assets/_Project/Scripts/Systems/Shop/ShopContentIds.cs"
    local events="Assets/_Project/Scripts/Systems/Shop/ShopEvents.cs"
    local system="Assets/_Project/Scripts/Systems/Shop/ShopSystem.cs"
    local panel="Assets/_Project/Scripts/Systems/UI/Shop/ShopPanelView.cs"
    local npc="Assets/_Project/Scripts/Entities/NPC/NPCController.cs"
    local npc_bootstrap="Assets/_Project/Scripts/Entities/NPC/NpcRuntimeBootstrap.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0305PlayModeTests.cs"

    for file_path in \
        "${contract}" "${ids}" "${events}" "${system}" "${panel}" \
        "${npc}" "${npc_bootstrap}" "${scene_bootstrap}" \
        "${ui_bootstrap}" "${tests}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'bool Buy\(string npcId, string itemId, int count\)' "${contract}"
    assert_pattern 'bool Sell\(int inventorySlot, int count\)' "${contract}"
    assert_pattern 'npc_zhanggui' "${ids}"
    assert_pattern 'item_potion_heal_01' docs/09_CONTENT.md
    assert_pattern 'G03-05 张掌柜固定货单' docs/09_CONTENT.md
    assert_pattern 'public int BuyPrice' Assets/_Project/Scripts/Data/ScriptableObjects/ItemData.cs
    assert_pattern 'BuyPrice = 10' Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs
    assert_pattern 'BuyPrice = 25' Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs
    assert_pattern 'BuyPrice = 80' Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs
    assert_pattern 'AcquireSource\.Shop' "${system}"
    assert_pattern 'ShopEvents\.TransactionCompleted' "${system}"
    assert_pattern 'TrySaveModule\(InventoryManager\.SaveModuleName\)' "${system}"
    assert_pattern 'AddComponent<ShopSystem>' "${scene_bootstrap}"
    assert_pattern 'AddComponent<ShopPanelView>' "${ui_bootstrap}"
    assert_pattern 'VendorInteractionPromptLocalizationKey' "${npc}"
    assert_pattern 'EnsureZhangguiForScene' "${npc_bootstrap}"
    assert_pattern 'OnShopTransactionCompleted' docs/02_ARCHITECTURE.md
    assert_pattern 'ShopTransactionInfo' docs/03_DATA_LAYER.md
    assert_pattern 'ui_shop_insufficient_funds' docs/09_CONTENT.md

    assert_pattern 'ZhangguiOffersAuthoritativeStockAndInteractionOpensPanel' "${tests}"
    assert_pattern 'BuyDeductsSpiritStonesAddsItemPublishesAndPersists' "${tests}"
    assert_pattern 'InsufficientSpiritStonesFailsWithoutChangingInventory' "${tests}"
    assert_pattern 'SellUsesSellPriceAndPersistsInventoryAndCurrencyTogether' "${tests}"
    assert_pattern 'FullInventoryRejectsPurchaseWithoutCharging' "${tests}"
    assert_pattern 'InvalidBoundAndOverflowingTransactionsDoNotMutateState' "${tests}"

    printf 'G03-05 static validation passed.\n'
}

run_static_validation

if [[ "${STATIC_ONLY}" == true ]]; then
    exit 0
fi

if [[ ! -x "${UNITY_EDITOR}" ]]; then
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

run_unity_tests() {
    local platform="$1"
    local filter="$2"
    local result_name="$3"
    local result_path="${RESULTS_DIR}/${result_name}.xml"
    local log_path="${RESULTS_DIR}/${result_name}.log"
    local unity_exit=0

    rm -f "${result_path}" "${log_path}"
    "${UNITY_EDITOR}" \
        -batchmode \
        -nographics \
        -projectPath "${PROJECT_ROOT}" \
        -runTests \
        -testPlatform "${platform}" \
        -testFilter "${filter}" \
        -testResults "${result_path}" \
        -logFile "${log_path}" || unity_exit=$?

    if [[ ! -f "${result_path}" ]] \
        || ! rg -q '<test-run .*result="Passed".*failed="0"' \
            "${result_path}"; then
        printf '%s validation failed (Unity exit %s). See %s\n' \
            "${platform}" "${unity_exit}" "${log_path}" >&2
        if [[ "${unity_exit}" -eq 0 ]]; then
            unity_exit=1
        fi
        return "${unity_exit}"
    fi

    if [[ "${unity_exit}" -ne 0 ]]; then
        printf '%s tests passed; ignoring Unity shutdown exit %s.\n' \
            "${platform}" "${unity_exit}"
    fi
}

run_unity_tests \
    EditMode \
    Wendao.Tests.EditMode.Data \
    G03-05-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.GVS03PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0305PlayModeTests' \
    G03-05-playmode

printf 'G03-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
