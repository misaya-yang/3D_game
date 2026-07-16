#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
readonly CACHE_ROOT="${WENDAO_ASSET_CACHE:-/tmp/wendao-assets}"
readonly RESOURCE_ROOT="${PROJECT_ROOT}/Assets/_Project/Resources/Art/Budget"
readonly LICENSE_ROOT="${PROJECT_ROOT}/Assets/_Project/Art/ThirdParty/Licenses"
readonly HASH_FILE="${PROJECT_ROOT}/Assets/_Project/Art/ThirdParty/SHA256SUMS"

readonly NATURE_URL="https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip"
readonly DUNGEON_URL="https://kenney.nl/media/pages/assets/modular-dungeon-kit/7bed87605b-1771926065/kenney_modular-dungeon-kit_1.0.zip"
readonly NATURE_ZIP="${CACHE_ROOT}/kenney_nature-kit.zip"
readonly DUNGEON_ZIP="${CACHE_ROOT}/kenney_modular-dungeon-kit.zip"
readonly CHARACTER_CACHE="${CACHE_ROOT}/rpg-characters"
readonly CREATURE_CACHE="${CACHE_ROOT}/creatures"
readonly SURFACE_CACHE="${CACHE_ROOT}/surfaces"

download_http() {
    local url="$1"
    local output="$2"
    if [[ -s "${output}" ]]; then
        return
    fi

    mkdir -p "$(dirname "${output}")"
    curl -L --fail --retry 3 --silent --show-error "${url}" -o "${output}"
}

download_drive() {
    local file_id="$1"
    local output="$2"
    if [[ -s "${output}" ]]; then
        return
    fi

    command -v uvx >/dev/null 2>&1 || {
        printf 'uvx is required to download the curated Quaternius files.\n' >&2
        exit 2
    }
    mkdir -p "$(dirname "${output}")"
    uvx gdown --continue "${file_id}" -O "${output}"
}

extract_fbx() {
    local archive="$1"
    local entry="$2"
    local destination="$3"
    mkdir -p "${destination}"
    unzip -j -o "${archive}" "${entry}" -d "${destination}" >/dev/null
}

download_http "${NATURE_URL}" "${NATURE_ZIP}"
download_http "${DUNGEON_URL}" "${DUNGEON_ZIP}"

mkdir -p \
    "${RESOURCE_ROOT}/Characters" \
    "${RESOURCE_ROOT}/Creatures" \
    "${RESOURCE_ROOT}/Nature" \
    "${RESOURCE_ROOT}/Dungeon" \
    "${RESOURCE_ROOT}/Surfaces" \
    "${LICENSE_ROOT}"

# name | FBX Google Drive id | texture Google Drive id
while IFS='|' read -r name fbx_id texture_id; do
    [[ -n "${name}" ]] || continue
    download_drive \
        "${fbx_id}" \
        "${CHARACTER_CACHE}/FBX/${name}.fbx"
    download_drive \
        "${texture_id}" \
        "${CHARACTER_CACHE}/Textures/${name}.png"
    cp "${CHARACTER_CACHE}/FBX/${name}.fbx" \
        "${RESOURCE_ROOT}/Characters/${name}.fbx"
    cp "${CHARACTER_CACHE}/Textures/${name}.png" \
        "${RESOURCE_ROOT}/Characters/${name}.png"
done <<'CHARACTERS'
Monk|1XIIYXYHzVlNj4AQCEDHAAzjxxJmj5vfY|1-_sWggWNjaDgEorZbUsq5bBa3gCTcpp7
Cleric|1TOCxE63wHOp_ASUUUfC_PoWC69AtISM9|14Fb-SldYSG5jh5xpAT0hI9OpersdJULl
Ranger|1iYUg6pGsTz-NGssvTCSta6bQliauwBT3|1Tlb3LUroYaVVkhHiNJKuFaeVDpbEiNX9
Rogue|1YpjHvIbEBUnvA31wIZ3jGTAWc7JWONFI|1KTyBW6z2EYWeRILRzP6m5VTriDi-_H6e
Wizard|1XwY0kkcA-l4JqN0onIjpQciA_6Fi0AOB|1BjvH6-y33wFMMsnoFhuEPSNNuRCZdjgk
Warrior|1mPcA-6gGZYLiwD9gle7E1bPckGEkoapx|1aCbtzIG86g5VJz63pAZ0a6_00e8nexyW
CHARACTERS

download_drive \
    "1dc19T9_fxiy7jscseYp0lP2IhRi-e2b6" \
    "${CHARACTER_CACHE}/License.txt"
cp "${CHARACTER_CACHE}/License.txt" \
    "${LICENSE_ROOT}/Quaternius_RPG_Character_Pack_CC0.txt"

download_drive \
    "128WmNfZthQqZ1OZpinWCAJ8IgvjNcoSm" \
    "${CREATURE_CACHE}/Wolf.fbx"
download_drive \
    "1F2uy8T2fRpdc6gZ4mnS02_C2E63WvKtn" \
    "${CREATURE_CACHE}/Ultimate_Animated_Animal_Pack_License.txt"
cp "${CREATURE_CACHE}/Wolf.fbx" \
    "${RESOURCE_ROOT}/Creatures/Wolf.fbx"
cp "${CREATURE_CACHE}/Ultimate_Animated_Animal_Pack_License.txt" \
    "${LICENSE_ROOT}/Quaternius_Ultimate_Animated_Animal_Pack_CC0.txt"

download_drive \
    "1j55qAtUkuKb6I56SXJ00p0OMCLm8NqpR" \
    "${CREATURE_CACHE}/Goleling_Evolved.fbx"
download_drive \
    "1GYmHXJkgtkbO__lIm804FsB0ZpKLhyN6" \
    "${CREATURE_CACHE}/Orc_Skull.fbx"
download_drive \
    "1CtLGgAKj-6a6GGNVNQT7GRPM7uyo9Fj8" \
    "${CREATURE_CACHE}/Goleling_Evolved.png"
download_drive \
    "16GqsDGESyEOfRbc4dS7EqAwkIUSIW4_y" \
    "${CREATURE_CACHE}/Ultimate_Monsters_License.txt"
cp "${CREATURE_CACHE}/Goleling_Evolved.fbx" \
    "${RESOURCE_ROOT}/Creatures/Goleling_Evolved.fbx"
cp "${CREATURE_CACHE}/Goleling_Evolved.png" \
    "${RESOURCE_ROOT}/Creatures/Goleling_Evolved.png"
cp "${CREATURE_CACHE}/Ultimate_Monsters_License.txt" \
    "${LICENSE_ROOT}/Quaternius_Ultimate_Monsters_CC0.txt"

while IFS='|' read -r name url; do
    [[ -n "${name}" ]] || continue
    download_http "${url}" "${SURFACE_CACHE}/${name}.jpg"
    cp "${SURFACE_CACHE}/${name}.jpg" \
        "${RESOURCE_ROOT}/Surfaces/${name}.jpg"
done <<'SURFACES'
grass_path_2_diff_1k|https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/grass_path_2/grass_path_2_diff_1k.jpg
forest_ground_04_diff_1k|https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/forest_ground_04/forest_ground_04_diff_1k.jpg
rocky_terrain_diff_1k|https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/rocky_terrain/rocky_terrain_diff_1k.jpg
SURFACES

while IFS= read -r name; do
    [[ -n "${name}" ]] || continue
    extract_fbx \
        "${NATURE_ZIP}" \
        "Models/FBX format/${name}.fbx" \
        "${RESOURCE_ROOT}/Nature"
done <<'NATURE_MODELS'
tree_default
tree_oak
tree_pineTallA
tree_pineTallB
tree_pineSmallA
tree_tall
tree_thin
tree_small
rock_largeA
rock_largeC
rock_largeE
rock_smallA
rock_smallC
rock_smallF
rock_tallA
rock_tallC
stone_largeB
stone_smallB
plant_bush
plant_bushDetailed
grass_large
grass_leafsLarge
flower_purpleA
flower_yellowB
crops_bambooStageB
mushroom_redGroup
bridge_wood
campfire_stones
log_stack
path_stone
path_stoneCircle
pot_large
pot_small
sign
fence_simple
fence_gate
statue_columnDamaged
statue_obelisk
tent_detailedOpen
cliff_rock
cliff_cave_rock
NATURE_MODELS

unzip -p "${NATURE_ZIP}" License.txt \
    > "${LICENSE_ROOT}/Kenney_Nature_Kit_CC0.txt"

while IFS= read -r name; do
    [[ -n "${name}" ]] || continue
    extract_fbx \
        "${DUNGEON_ZIP}" \
        "Models/FBX format/${name}.fbx" \
        "${RESOURCE_ROOT}/Dungeon"
done <<'DUNGEON_MODELS'
corridor
corridor-corner
corridor-junction
corridor-wide
gate-door
gate-metal-bars
room-small
room-small-variation
room-wide
room-large
stairs
template-wall-detail-a
DUNGEON_MODELS

unzip -j -o \
    "${DUNGEON_ZIP}" \
    "Models/FBX format/Textures/colormap.png" \
    -d "${RESOURCE_ROOT}/Dungeon" >/dev/null
unzip -p "${DUNGEON_ZIP}" License.txt \
    > "${LICENSE_ROOT}/Kenney_Modular_Dungeon_Kit_CC0.txt"

temporary_hash_file="${HASH_FILE}.tmp"
: > "${temporary_hash_file}"
find \
    "${RESOURCE_ROOT}" \
    "${LICENSE_ROOT}" \
    -type f \
    ! -name '*.meta' \
    ! -name 'SHA256SUMS' \
    -print \
    | LC_ALL=C sort \
    | while IFS= read -r file; do
        hash="$(shasum -a 256 "${file}" | awk '{print $1}')"
        relative="${file#${PROJECT_ROOT}/}"
        printf '%s  %s\n' "${hash}" "${relative}"
    done > "${temporary_hash_file}"
mv "${temporary_hash_file}" "${HASH_FILE}"

printf 'Budget art assets synchronized: %s\n' "${RESOURCE_ROOT}"
printf 'Integrity manifest written: %s\n' "${HASH_FILE}"
