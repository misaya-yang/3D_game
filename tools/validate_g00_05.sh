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

run_static_validation() {
    "${SCRIPT_DIR}/validate_g00_04.sh" --static-only

    local core_files=(
        Assets/_Project/Scripts/Core/SafeBehaviour.cs
        Assets/_Project/Scripts/Core/ObjectPool.cs
    )
    local data_files=(
        Assets/_Project/Scripts/Data/Save/JsonStorage.cs
        Assets/_Project/Scripts/Data/Save/SaveData.cs
        Assets/_Project/Scripts/Data/Save/SaveManager.cs
        Assets/_Project/Scripts/Data/Config/RealmConfig.cs
        Assets/_Project/Scripts/Data/Config/SpiritRootConfig.cs
        Assets/_Project/Scripts/Data/Config/BodyRefinementConfig.cs
        Assets/_Project/Scripts/Data/Config/CraftLevelConfig.cs
        Assets/_Project/Scripts/Data/Config/FormulaLibrary.cs
        Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs
    )
    local config_files=(
        Assets/StreamingAssets/Config/RealmConfig.json
        Assets/StreamingAssets/Config/SpiritRootConfig.json
        Assets/StreamingAssets/Config/BodyRefinementConfig.json
        Assets/StreamingAssets/Config/CraftLevelConfig.json
    )
    local test_files=(
        Assets/_Project/Tests/RuntimeSupport/SafeBehaviourProbe.cs
        Assets/_Project/Tests/EditMode/SafeBehaviourTests.cs
        Assets/_Project/Tests/RuntimeSupport/PoolProbe.cs
        Assets/_Project/Tests/EditMode/ObjectPoolTests.cs
        Assets/_Project/Tests/EditMode/Data/SaveManagerTests.cs
        Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs
    )

    local file_path
    for file_path in "${core_files[@]}" "${data_files[@]}" "${config_files[@]}" "${test_files[@]}"; do
        assert_file "${file_path}"
    done

    [[ "$(jq -r '.dependencies["com.unity.nuget.newtonsoft-json"]' "${PROJECT_ROOT}/Packages/manifest.json")" == '3.2.2' ]]
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/Assets/_Project/Scripts/Data/Wendao.Data.asmdef")" == '["Wendao.Core","Unity.Newtonsoft.Json"]' ]]

    for file_path in "${config_files[@]}"; do
        jq empty "${PROJECT_ROOT}/${file_path}"
    done

    local realm_json="${PROJECT_ROOT}/${config_files[0]}"
    local root_json="${PROJECT_ROOT}/${config_files[1]}"
    local body_json="${PROJECT_ROOT}/${config_files[2]}"
    local craft_json="${PROJECT_ROOT}/${config_files[3]}"
    [[ "$(jq '.realms | length' "${realm_json}")" -eq 4 ]]
    [[ "$(jq '.realms[] | select(.realm == 1) | .subStages' "${realm_json}")" -eq 9 ]]
    [[ "$(jq '.realms[] | select(.realm == 1) | .xpPerSubStage[0]' "${realm_json}")" -eq 100 ]]
    [[ "$(jq -r '.realms[] | select(.realm == 1) | .breakthroughToNext.requiredItemId' "${realm_json}")" == 'item_pill_foundation' ]]
    [[ "$(jq -r '.roots[] | select(.type == "Water") | .elementBonus.Water' "${root_json}")" == '0.15' ]]
    [[ "$(jq -r '.roots[] | select(.type == "Water") | .elementBonus.Ice' "${root_json}")" == '0.1' ]]
    [[ "$(jq '.defaultPickable | length' "${root_json}")" -eq 5 ]]
    if jq -e '.defaultPickable | any(. == "Heaven" or . == "Waste")' "${root_json}" >/dev/null; then
        printf 'Heaven and Waste roots must not be directly pickable.\n' >&2
        exit 1
    fi
    [[ "$(jq '.levels | length' "${body_json}")" -eq 5 ]]
    [[ "$(jq '.alchemy | length' "${craft_json}")" -eq 10 ]]

    if find "${PROJECT_ROOT}/Assets/StreamingAssets/Config" -maxdepth 1 \
        -type f -iname '*FeatureFlags*' -print -quit | rg -q .; then
        printf 'FeatureFlags runtime files are explicitly out of scope for the MVP.\n' >&2
        exit 1
    fi

    local save_manager="Assets/_Project/Scripts/Data/Save/SaveManager.cs"
    assert_pattern 'public const int SlotCount = 3;' "${save_manager}"
    assert_pattern 'public bool SaveGame\(int slot\)' "${save_manager}"
    assert_pattern 'public bool LoadGame\(int slot\)' "${save_manager}"
    assert_pattern 'public bool DeleteSave\(int slot\)' "${save_manager}"
    assert_pattern 'public SaveMetadata GetMetadata\(int slot\)' "${save_manager}"
    assert_pattern 'public SaveMetadata\[\] GetAllSaves\(\)' "${save_manager}"
    assert_pattern 'public void AutoSave\(\)' "${save_manager}"
    assert_pattern 'public void SaveModule\(string moduleName\)' "${save_manager}"
    assert_pattern 'MinimumAutoSaveIntervalSeconds = 60d' "${save_manager}"
    assert_pattern 'checkpoint < 0 \|\| checkpoint > 4' "${save_manager}"
    assert_pattern 'Metadata is written last' "${save_manager}"

    local save_data="Assets/_Project/Scripts/Data/Save/SaveData.cs"
    assert_pattern 'public const int CurrentVersion = 1;' "${save_data}"
    assert_pattern 'public List<string> TutorialsCompleted' "${save_data}"
    assert_pattern 'public Dictionary<string, int> DungeonCheckpoint' "${save_data}"
    assert_pattern 'public bool IsCorrupted;' "${save_data}"
    assert_pattern '\[JsonIgnore\] public bool IsCorrupted;' "${save_data}"

    local json_storage="Assets/_Project/Scripts/Data/Save/JsonStorage.cs"
    assert_pattern 'CamelCasePropertyNamesContractResolver' "${json_storage}"
    assert_pattern 'File\.Replace\(tempPath, path, backupPath, true\)' "${json_storage}"
    assert_pattern 'StringEnumConverter' "${json_storage}"

    local config_database="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    assert_pattern 'public RealmConfig Realm \{ get; private set; \}' "${config_database}"
    assert_pattern 'public SpiritRootConfig SpiritRoot \{ get; private set; \}' "${config_database}"
    assert_pattern 'public bool IsSafeMode \{ get; private set; \}' "${config_database}"
    assert_pattern 'public void LoadAll\(\)' "${config_database}"
    assert_pattern 'public ItemData GetItem\(string id\)' "${config_database}"
    assert_pattern 'public SkillData GetSkill\(string id\)' "${config_database}"
    assert_pattern 'Water spirit root must retain Water 0\.15 and Ice 0\.10' "${config_database}"

    assert_pattern 'public abstract class SafeBehaviour : MonoBehaviour' "${core_files[0]}"
    assert_pattern 'protected virtual void SafeStart\(\)' "${core_files[0]}"
    assert_pattern 'catch \(Exception exception\)' "${core_files[0]}"
    assert_pattern 'public sealed class ObjectPool<T> where T : Component' "${core_files[1]}"
    assert_pattern 'public T Get\(\)' "${core_files[1]}"
    assert_pattern 'public void Return\(T instance\)' "${core_files[1]}"
    assert_pattern 'public void Prewarm\(int count\)' "${core_files[1]}"
    assert_pattern 'public void Clear\(\)' "${core_files[1]}"

    assert_pattern 'SaveAndLoad_RoundTripProfileWorldAndMetadataAcrossSlot' "${test_files[4]}"
    assert_pattern 'DamagedRequiredFile_MarksSlotCorrupted' "${test_files[4]}"
    assert_pattern 'DefaultConfigs_LoadAllAuthoritativeTables' "${test_files[5]}"
    assert_pattern 'GetAndReturn_ReuseOwnedInstanceAndInvokeLifecycle' "${test_files[3]}"
    assert_pattern 'SafeStart_ContainsExceptionAndDisablesFailedComponent' "${test_files[1]}"

    printf 'G00-05 static validation passed. Unity compilation and tests remain pending.\n'
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
    -testResults "${RESULTS_DIR}/G00-05-editmode.xml" \
    -logFile "${RESULTS_DIR}/G00-05-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode \
    -testResults "${RESULTS_DIR}/G00-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/G00-05-playmode.log"

printf 'G00-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
