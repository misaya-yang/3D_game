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
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    }
}

cd "${PROJECT_ROOT}"
"${SCRIPT_DIR}/check_docs_consistency.sh"
bash -n "${SCRIPT_DIR}/art/sync_budget_assets.sh"
bash -n "${SCRIPT_DIR}/validate_g09_02.sh"

required_files=(
    Assets/_Project/Art/ThirdParty/SHA256SUMS
    Assets/_Project/Art/ThirdParty/Licenses/Quaternius_RPG_Character_Pack_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Quaternius_Ultimate_Animated_Animal_Pack_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Quaternius_Ultimate_Monsters_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Kenney_Nature_Kit_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Kenney_Modular_Dungeon_Kit_CC0.txt
    Assets/_Project/Art/ThirdParty/Licenses/Poly_Haven_CC0_Sources.txt
    Assets/_Project/Resources/Art/Budget/Characters/Warrior.fbx
    Assets/_Project/Resources/Art/Budget/Characters/Warrior.png
    Assets/_Project/Resources/Art/Budget/Creatures/Wolf.fbx
    Assets/_Project/Resources/Art/Budget/Creatures/Goleling_Evolved.fbx
    Assets/_Project/Resources/Art/Budget/Creatures/Goleling_Evolved.png
    Assets/_Project/Resources/Art/Budget/Surfaces/grass_path_2_diff_1k.jpg
    Assets/_Project/Resources/Art/Budget/Surfaces/forest_ground_04_diff_1k.jpg
    Assets/_Project/Resources/Art/Budget/Surfaces/rocky_terrain_diff_1k.jpg
    Assets/_Project/Scripts/Systems/World/CangwuNavigationSurface.cs
    Assets/_Project/Tests/PlayMode/VerticalSlice/G0902PlayModeTests.cs
    docs/art/G09-02_VISUAL_AUDIT.md
    docs/art/THIRD_PARTY_ASSETS.md
)
for file_path in "${required_files[@]}"; do
    assert_file "${file_path}"
done

[[ "$(find Assets/_Project/Resources/Art/Budget -type f -name '*.fbx' | wc -l | tr -d ' ')" -ge 61 ]] || {
    printf 'Expected at least 61 curated FBX models.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Resources/Art/Budget/Characters -type f -name '*.png' | wc -l | tr -d ' ')" -ge 6 ]] || {
    printf 'Expected at least 6 curated character textures.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Resources/Art/Budget/Creatures -type f -name '*.fbx' | wc -l | tr -d ' ')" -ge 2 ]] || {
    printf 'Expected wolf and boss creature FBX models.\n' >&2
    exit 1
}
[[ "$(find Assets/_Project/Resources/Art/Budget/Surfaces -type f -name '*.jpg' | wc -l | tr -d ' ')" -ge 3 ]] || {
    printf 'Expected three curated terrain diffuse textures.\n' >&2
    exit 1
}
[[ "$(find docs/art/previews/g09-02/before -type f -name '*.png' | wc -l | tr -d ' ')" -ge 3 ]] || {
    printf 'Expected three baseline map screenshots.\n' >&2
    exit 1
}
[[ "$(find docs/art/previews/g09-02/after -type f -name '*.png' | wc -l | tr -d ' ')" -ge 5 ]] || {
    printf 'Expected final map and character screenshots.\n' >&2
    exit 1
}

shasum -a 256 -c Assets/_Project/Art/ThirdParty/SHA256SUMS >/dev/null
assert_pattern 'BudgetMaterialProfile\.Stone' \
    Assets/_Project/Scripts/Entities/Visuals/BudgetWorldArtBootstrap.cs
assert_pattern 'forceRenderingOff' \
    Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs
assert_pattern 'class CangwuNavigationSurface' \
    Assets/_Project/Scripts/Systems/World/CangwuNavigationSurface.cs
assert_pattern 'RequiredRefinementResources' \
    Assets/_Project/Scripts/Entities/Visuals/BudgetArtCatalog.cs
assert_pattern 'class G0902PlayModeTests' \
    Assets/_Project/Tests/PlayMode/VerticalSlice/G0902PlayModeTests.cs
if ! rg -U -q \
    'id: G09-02\nphase: 9\nstatus: (in_progress|implemented|done)' \
    docs/10_GOALS.md; then
    printf 'G09-02 must be active or delivered in docs/10_GOALS.md.\n' >&2
    exit 1
fi

printf 'G09-02 static validation passed.\n'
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0902PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0102PlayModeTests' \
    -testResults "${RESULTS_DIR}/G09-02-targeted-playmode.xml" \
    -logFile "${RESULTS_DIR}/G09-02-targeted-playmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testResults "${RESULTS_DIR}/G09-02-full-editmode.xml" \
    -logFile "${RESULTS_DIR}/G09-02-full-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testResults "${RESULTS_DIR}/G09-02-full-playmode.xml" \
    -logFile "${RESULTS_DIR}/G09-02-full-playmode.log"

"${SCRIPT_DIR}/build_macos.sh"
"${SCRIPT_DIR}/smoke_macos_player.sh"

printf 'G09-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
