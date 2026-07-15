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
    "${SCRIPT_DIR}/validate_g00_03.sh" --static-only

    local enum_file="Assets/_Project/Scripts/Data/Enums/GameEnums.cs"
    local event_file="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local damage_request_file="Assets/_Project/Scripts/Data/Runtime/DamageRequest.cs"
    local schema_dir="Assets/_Project/Scripts/Data/ScriptableObjects"
    local test_asmdef="Assets/_Project/Tests/EditMode/Data/Wendao.Data.EditModeTests.asmdef"
    local tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"

    local required_files=(
        "${enum_file}"
        "${event_file}"
        "${damage_request_file}"
        "${schema_dir}/ItemData.cs"
        "${schema_dir}/EquipmentData.cs"
        "${schema_dir}/SkillData.cs"
        "${schema_dir}/QuestData.cs"
        "${schema_dir}/EnemyData.cs"
        "${schema_dir}/NPCData.cs"
        "${schema_dir}/CraftRecipeData.cs"
        "${schema_dir}/MapData.cs"
        "${schema_dir}/DialogueData.cs"
        "${schema_dir}/AchievementData.cs"
        "${schema_dir}/TitleData.cs"
        "${schema_dir}/MountData.cs"
        "${schema_dir}/StatusEffectData.cs"
        "${schema_dir}/SerendipityData.cs"
        "${test_asmdef}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    jq empty "${PROJECT_ROOT}/${test_asmdef}"
    [[ "$(jq -c '.references' "${PROJECT_ROOT}/${test_asmdef}")" == '["Wendao.Core","Wendao.Data"]' ]]
    [[ "$(jq -c '.includePlatforms' "${PROJECT_ROOT}/${test_asmdef}")" == '["Editor"]' ]]
    [[ "$(jq -r '.optionalUnityReferences[]' "${PROJECT_ROOT}/${test_asmdef}")" == 'TestAssemblies' ]]

    local enums=(
        DamageType ElementType RealmType SpiritRootType EquipmentSlot ItemType
        ItemRarity SkillType SkillElement QuestType QuestStatus ObjectiveType
        EnemyRank CraftType XpSourceType AcquireSource WeatherId BodyLevel
        UseEffectType TelegraphShape PlayerState
    )
    local enum_name
    for enum_name in "${enums[@]}"; do
        assert_pattern "public enum ${enum_name}" "${enum_file}"
    done
    assert_pattern 'ReachRealm' "${enum_file}"

    if rg -q 'enum[[:space:]]+GameState' "${PROJECT_ROOT}/Assets/_Project/Scripts/Data"; then
        printf 'GameState must remain Core-owned; a duplicate was found under Data.\n' >&2
        exit 1
    fi

    local event_structs=(
        DamageInfo HealInfo DeathInfo EnemyDeathInfo XpGainInfo RealmChangeInfo
        ItemAcquireInfo ItemUseInfo EquipmentChangeInfo EquipmentUpgradeInfo
        SkillInfo SkillCastInfo SkillUpgradeInfo QuestInfo QuestProgressInfo
        DialogueInfo CraftResultInfo AffectionInfo AchievementInfo TitleInfo
        DayNightInfo WeatherInfo MapInfo BossPhaseInfo MountInfo FlightInfo
        SerendipityInfo TutorialPromptInfo
    )
    local struct_name
    for struct_name in "${event_structs[@]}"; do
        assert_pattern "public struct ${struct_name}" "${event_file}"
    done
    assert_pattern 'public string SkillId;' "${event_file}"
    assert_pattern 'public string LastHitSkillId;' "${event_file}"
    assert_pattern 'public struct DamageRequest' "${damage_request_file}"

    local scriptable_objects=(
        ItemData EquipmentData SkillData QuestData EnemyData NPCData
        CraftRecipeData MapData DialogueData AchievementData TitleData
        MountData StatusEffectData SerendipityData
    )
    local scriptable_name
    for scriptable_name in "${scriptable_objects[@]}"; do
        if ! rg -q "public class ${scriptable_name} : ScriptableObject" \
            "${PROJECT_ROOT}/${schema_dir}"; then
            printf 'ScriptableObject schema missing: %s\n' "${scriptable_name}" >&2
            exit 1
        fi
    done

    local create_menu_count
    create_menu_count="$(rg -c '\[CreateAssetMenu\(' "${PROJECT_ROOT}/${schema_dir}"/*.cs | awk -F: '{ total += $2 } END { print total + 0 }')"
    if [[ "${create_menu_count}" -ne 14 ]]; then
        printf 'Expected 14 CreateAssetMenu declarations, got %s.\n' "${create_menu_count}" >&2
        exit 1
    fi

    assert_pattern 'public string SkillId;' "${schema_dir}/EnemyData.cs"
    assert_pattern 'public BossSkillTelegraph\[\] Telegraphs' "${schema_dir}/EnemyData.cs"
    assert_pattern 'public RealmType RequiredRealm = RealmType\.Mortal;' "${schema_dir}/SerendipityData.cs"
    assert_pattern 'public QuestReward Rewards' "${schema_dir}/SerendipityData.cs"
    assert_pattern 'public static StatBlock operator \+' "${schema_dir}/EquipmentData.cs"
    assert_pattern 'public StatBlock Multiply\(float multiplier\)' "${schema_dir}/EquipmentData.cs"

    assert_pattern 'ScriptableObjectSchemas_HaveExpectedCreateMenusAndCanInstantiate' "${tests}"
    assert_pattern 'EnumSchemas_MatchAuthoritativeNamesAndOrder' "${tests}"
    assert_pattern 'PublicFieldSchemas_MatchAuthoritativeContracts' "${tests}"
    assert_pattern 'SchemaDefaults_AreSafeAndMatchDocumentedValues' "${tests}"
    assert_pattern 'StatBlock_AddAndMultiply_OperateOnEveryPublicStat' "${tests}"

    if find "${PROJECT_ROOT}/Assets/_Project/ScriptableObjects" -type f -name '*.asset' -print -quit | rg -q .; then
        printf 'Concrete ScriptableObject assets are out of scope for G00-04.\n' >&2
        exit 1
    fi

    printf 'G00-04 static validation passed. Unity compilation and EditMode tests remain pending.\n'
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

UNITY_EDITOR="${UNITY_EDITOR}" "${SCRIPT_DIR}/validate_g00_03.sh"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G00-04-editmode.xml" \
    -logFile "${RESULTS_DIR}/G00-04-editmode.log"

printf 'G00-04 Unity validation passed. Results: %s\n' "${RESULTS_DIR}/G00-04-editmode.xml"
