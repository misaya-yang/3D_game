#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"
readonly BINDING_REPORT="${RESULTS_DIR}/G09-07-cultivator-bindings.json"
readonly BOSS_BINDING_REPORT="${RESULTS_DIR}/G09-07-stone-general-bindings.json"

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

cd "${PROJECT_ROOT}"
bash -n \
    tools/art/sync_character_sources.sh \
    tools/blender/run_character_pipeline.sh \
    tools/blender/run_mpfb_character_pipeline.sh \
    tools/validate_g09_07.sh
python3 -m py_compile \
    tools/blender/character_pipeline.py \
    tools/blender/quaternius_character_pipeline.py \
    tools/blender/audit_character_assets.py \
    tools/blender/build_stone_general.py \
    tools/blender/render_fbx_lineup.py

required_files=(
    Assets/_Project/Resources/Art/Budget/Characters/Cultivator.fbx
    Assets/_Project/Resources/Art/Budget/Characters/NpcGuard_Modular.fbx
    Assets/_Project/Resources/Art/Budget/Characters/NpcHealer_Modular.fbx
    Assets/_Project/Resources/Art/Budget/Characters/NpcHermit_Modular.fbx
    Assets/_Project/Resources/Art/Budget/Characters/Bandit_Modular.fbx
    Assets/_Project/Resources/Art/Budget/Creatures/StoneGeneral.fbx
    Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures/Skin.png
    Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures/Eyes.png
    Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures/Hair.png
    Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures/Robe.png
    Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures/Ranger.png
    Assets/_Project/Art/ThirdParty/Licenses/Quaternius_Universal_Base_Characters_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Quaternius_Modular_Character_Outfits_Fantasy_CC0.txt
    ArtSource/Characters/Player/Cultivator_Modular_v2.blend
    ArtSource/Characters/Player/Cultivator_Modular_v2_manifest.json
    ArtSource/Characters/NPC/NpcGuard_Modular_v1.blend
    ArtSource/Characters/NPC/NpcGuard_Modular_v1_manifest.json
    ArtSource/Characters/NPC/NpcHealer_Modular_v1.blend
    ArtSource/Characters/NPC/NpcHealer_Modular_v1_manifest.json
    ArtSource/Characters/NPC/NpcHermit_Modular_v1.blend
    ArtSource/Characters/NPC/NpcHermit_Modular_v1_manifest.json
    ArtSource/Characters/Enemy/Bandit_Modular_v1.blend
    ArtSource/Characters/Enemy/Bandit_Modular_v1_manifest.json
    ArtSource/Characters/Boss/StoneGeneral_OpenSource_v2.blend
    ArtSource/Characters/Boss/StoneGeneral_OpenSource_v2_manifest.json
    Assets/_Project/Scripts/Entities/Visuals/ModularCharacterMaterialUtility.cs
    Assets/_Project/Scripts/Entities/Visuals/ModularCharacterStyle.cs
    Assets/_Project/Scripts/Entities/Visuals/StoneGeneralStyle.cs
    docs/art/G09-07_CHARACTER_AUDIT.md
    docs/art/g09-07-character-assets.json
    docs/art/previews/g09-07/final/01-cultivator-modular-v2.png
    docs/art/previews/g09-07/boss/02-stone-general-open-source-v2.png
    tools/art/sync_character_sources.sh
    tools/blender/config/g09_07_modular_characters.json
    tools/blender/quaternius_character_pipeline.py
    tools/blender/build_stone_general.py
    tools/blender/render_fbx_lineup.py
)
for file_path in "${required_files[@]}"; do
    assert_file "${file_path}"
done

validate_manifest() {
    local manifest="$1"
    local profile="$2"
    local accessory="$3"
    local minimum_actions="$4"
    jq -e \
        --arg profile "${profile}" \
        --arg accessory "${accessory}" \
        --argjson minimum_actions "${minimum_actions}" '
        .schema_version == 2
        and .profile == $profile
        and .validation.total_triangles <= 52000
        and .validation.bone_count == 44
        and .validation.ground_error <= 0.001
        and (.validation.actions | length) >= $minimum_actions
        and any(.validation.objects[]; .name == "Cultivator_Head")
        and any(.validation.objects[]; .name == "Cultivator_Hair")
        and any(.validation.objects[]; .name == $accessory)
    ' "${PROJECT_ROOT}/${manifest}" >/dev/null
}

validate_manifest \
    ArtSource/Characters/Player/Cultivator_Modular_v2_manifest.json \
    cultivator Cultivator_Jian 11
validate_manifest \
    ArtSource/Characters/NPC/NpcGuard_Modular_v1_manifest.json \
    npc_guard Modular_Guard_Bow 12
validate_manifest \
    ArtSource/Characters/NPC/NpcHealer_Modular_v1_manifest.json \
    npc_healer Modular_Healer_Staff 12
validate_manifest \
    ArtSource/Characters/NPC/NpcHermit_Modular_v1_manifest.json \
    npc_hermit Modular_Hermit_Staff 12
validate_manifest \
    ArtSource/Characters/Enemy/Bandit_Modular_v1_manifest.json \
    human_enemy Modular_Bandit_Dagger 11

jq -e '
    .schema_version == 2
    and .profile == "stone_general"
    and .validation.total_triangles <= 18000
    and .validation.bone_count >= 50
    and .validation.ground_error <= 0.002
    and (.validation.actions | length) >= 12
    and any(.validation.objects[]; . == "StoneGeneral_Body")
    and any(.validation.objects[]; . == "StoneGeneral_Maul")
    and any(.validation.objects[]; . == "StoneGeneral_Core")
    and any(.validation.objects[]; . == "StoneGeneral_Crown")
' \
    ArtSource/Characters/Boss/StoneGeneral_OpenSource_v2_manifest.json \
    >/dev/null

shasum -a 256 -c \
    Assets/_Project/Art/ThirdParty/SHA256SUMS >/dev/null

if ! rg -U -q \
    'id: G09-07\nphase: 9\nstatus: (in_progress|implemented|done)' \
    docs/10_GOALS.md; then
    printf 'G09-07 must be active or delivered in docs/10_GOALS.md.\n' >&2
    exit 1
fi

printf 'G09-07 static validation passed.\n'
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' \
        "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -quit \
    -projectPath "${PROJECT_ROOT}" \
    -executeMethod Wendao.Editor.CharacterAssetAudit.WriteG0907Reports \
    -logFile "${RESULTS_DIR}/G09-07-binding-audit.log"

jq -e '
    (.transformPaths | index("CultivatorArmature")) != null
    and (.transformPaths | index("Cultivator_Head")) != null
    and (.transformPaths | index("Cultivator_Hair")) != null
    and (.transformPaths | index("Cultivator_Jian")) != null
    and (.clips | length) >= 11
    and all(.clips[]; .legacy == true)
' "${BINDING_REPORT}" >/dev/null

jq -e '
    (.transformPaths | index("StoneGeneralArmature")) != null
    and (.transformPaths | index("StoneGeneral_Body")) != null
    and (.transformPaths | index("StoneGeneral_Maul")) != null
    and (.transformPaths | index("StoneGeneral_Core")) != null
    and (.clips | length) >= 12
    and all(.clips[]; .legacy == true)
    and any(.clips[]; .maximumFloatCurveDelta > 0.01)
' "${BOSS_BINDING_REPORT}" >/dev/null

"${UNITY_EDITOR}" \
    -batchmode -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0907PlayModeTests' \
    -testResults "${RESULTS_DIR}/G09-07-playmode.xml" \
    -logFile "${RESULTS_DIR}/G09-07-playmode.log"

python3 - "${RESULTS_DIR}/G09-07-playmode.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
if root.attrib.get("result") != "Passed":
    raise SystemExit(
        "G09-07 PlayMode failed: "
        + str({key: root.attrib.get(key) for key in ("total", "passed", "failed")})
    )
PY

"${UNITY_EDITOR}" \
    -batchmode \
    -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter \
    'Wendao.Tests.PlayMode.VerticalSlice.G0907PlayModeTests.TwentyCharacterCombatLineupMeetsPerformanceBudget' \
    -testResults "${RESULTS_DIR}/G09-07-performance.xml" \
    -logFile "${RESULTS_DIR}/G09-07-performance.log"

python3 - "${RESULTS_DIR}/G09-07-performance.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
if root.attrib.get("result") != "Passed":
    raise SystemExit(
        "G09-07 performance failed: "
        + str({key: root.attrib.get(key) for key in ("total", "passed", "failed")})
    )
PY

printf 'G09-07 targeted Unity and Metal performance validation passed.\n'
