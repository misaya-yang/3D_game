#!/bin/sh

set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT_ROOT="$(CDPATH= cd -- "${SCRIPT_DIR}/../.." && pwd)"
CACHE_ROOT="${WENDAO_ASSET_CACHE:-/tmp/wendao-assets}/quaternius"
SELECTED_ROOT="${CACHE_ROOT}/selected"
METADATA_ROOT="${CACHE_ROOT}/metadata"
RESOURCE_TEXTURE_ROOT="${PROJECT_ROOT}/Assets/_Project/Resources/Art/Budget/Characters/CultivatorTextures"
RESOURCE_ROOT="${PROJECT_ROOT}/Assets/_Project/Resources/Art/Budget"
LICENSE_ROOT="${PROJECT_ROOT}/Assets/_Project/Art/ThirdParty/Licenses"
HASH_FILE="${PROJECT_ROOT}/Assets/_Project/Art/ThirdParty/SHA256SUMS"

BASE_SLUG="universal-base-characters"
BASE_ZIP="${CACHE_ROOT}/Universal Base Characters[Standard].zip"
BASE_SHA256="fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40"

OUTFIT_SLUG="modular-character-outfits-fantasy"
OUTFIT_ZIP="${CACHE_ROOT}/Modular Character Outfits - Fantasy[Standard].zip"
OUTFIT_SHA256="c3468b18871cc8c8f05ab14df7712baf22cb9f389cbd870babf130e595187f70"

sha256_file() {
    shasum -a 256 "$1" | awk '{print $1}'
}

download_itch_free_upload() {
    slug="$1"
    output="$2"
    expected_sha256="$3"
    metadata="${METADATA_ROOT}/${slug}"

    if [ -s "${output}" ]; then
        actual_sha256="$(sha256_file "${output}")"
        if [ "${actual_sha256}" = "${expected_sha256}" ]; then
            printf 'Character source cache ready: %s\n' "${output}"
            return
        fi

        printf 'Cached source hash differs: %s\n' "${output}" >&2
        printf 'Expected: %s\nActual:   %s\n' \
            "${expected_sha256}" \
            "${actual_sha256}" >&2
        printf 'Move the stale cache file aside, then rerun this script.\n' >&2
        exit 3
    fi

    mkdir -p "${CACHE_ROOT}" "${metadata}"

    purchase_url="https://quaternius.itch.io/${slug}/purchase"
    cookie_file="${metadata}/cookies.txt"
    purchase_html="${metadata}/purchase.html"
    download_json="${metadata}/download.json"
    download_html="${metadata}/download.html"
    file_json="${metadata}/file.json"
    partial="${output}.partial"

    curl -sS -L --fail \
        -c "${cookie_file}" \
        -b "${cookie_file}" \
        "${purchase_url}" \
        -o "${purchase_html}"
    csrf="$(
        perl -ne \
            'if (/meta name="csrf_token" value="([^"]+)/) { print $1; exit }' \
            "${purchase_html}"
    )"
    [ -n "${csrf}" ] || {
        printf 'Could not read itch.io CSRF token for %s\n' "${slug}" >&2
        exit 4
    }

    curl -sS --fail \
        -c "${cookie_file}" \
        -b "${cookie_file}" \
        -X POST \
        --data-urlencode "csrf_token=${csrf}" \
        -H 'X-Requested-With: XMLHttpRequest' \
        "https://quaternius.itch.io/${slug}/download_url" \
        -o "${download_json}"
    page_url="$(jq -r '.url // empty' "${download_json}")"
    [ -n "${page_url}" ] || {
        printf 'itch.io did not return a download page for %s\n' "${slug}" >&2
        exit 5
    }

    curl -sS --fail \
        -c "${cookie_file}" \
        -b "${cookie_file}" \
        "${page_url}" \
        -o "${download_html}"
    upload_id="$(
        sed -n 's/.*data-upload_id="\([0-9][0-9]*\)".*/\1/p' \
            "${download_html}" \
            | head -1
    )"
    csrf="$(
        perl -ne \
            'if (/meta name="csrf_token" value="([^"]+)/) { print $1; exit }' \
            "${download_html}"
    )"
    [ -n "${upload_id}" ] && [ -n "${csrf}" ] || {
        printf 'Could not resolve the free Standard upload for %s\n' \
            "${slug}" >&2
        exit 6
    }

    curl -sS --fail \
        -c "${cookie_file}" \
        -b "${cookie_file}" \
        -X POST \
        --data-urlencode "csrf_token=${csrf}" \
        "https://quaternius.itch.io/${slug}/file/${upload_id}?source=game_download&as_props=1" \
        -o "${file_json}"
    file_url="$(jq -r '.url // empty' "${file_json}")"
    [ -n "${file_url}" ] || {
        printf 'itch.io did not return a file URL for %s\n' "${slug}" >&2
        exit 7
    }

    curl --fail --location \
        --retry 8 \
        --retry-all-errors \
        --connect-timeout 20 \
        "${file_url}" \
        -o "${partial}"

    actual_sha256="$(sha256_file "${partial}")"
    if [ "${actual_sha256}" != "${expected_sha256}" ]; then
        printf 'Downloaded source hash differs for %s\n' "${slug}" >&2
        printf 'Expected: %s\nActual:   %s\n' \
            "${expected_sha256}" \
            "${actual_sha256}" >&2
        exit 8
    fi
    mv "${partial}" "${output}"
}

extract_one() {
    archive="$1"
    entry="$2"
    destination="$3"
    mkdir -p "${destination}"
    unzip -j -o "${archive}" "${entry}" -d "${destination}" >/dev/null
}

download_itch_free_upload \
    "${BASE_SLUG}" \
    "${BASE_ZIP}" \
    "${BASE_SHA256}"
download_itch_free_upload \
    "${OUTFIT_SLUG}" \
    "${OUTFIT_ZIP}" \
    "${OUTFIT_SHA256}"

BASE_PREFIX='Universal Base Characters\[Standard\]'
OUTFIT_PREFIX='Modular Character Outfits - Fantasy\[Standard\]'

extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Base Characters/Unity/Superhero_Male_FullBody.fbx" \
    "${SELECTED_ROOT}/base"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Hairstyles/Origin at 0/FBX (Unity)/Hair_Buns.fbx" \
    "${SELECTED_ROOT}/base"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Hairstyles/Origin at 0/FBX (Unity)/Hair_Long.fbx" \
    "${SELECTED_ROOT}/base"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Hairstyles/Origin at 0/FBX (Unity)/Hair_SimpleParted.fbx" \
    "${SELECTED_ROOT}/base"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Base Characters/Textures/T_Superhero_Male_Ligh.png" \
    "${SELECTED_ROOT}/textures"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Base Characters/Textures/T_Eye_Brown.png" \
    "${SELECTED_ROOT}/textures"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/Hairstyles/Textures/T_Hair_2_BaseColor.png" \
    "${SELECTED_ROOT}/textures"
extract_one \
    "${BASE_ZIP}" \
    "${BASE_PREFIX}/License_Standard.txt" \
    "${SELECTED_ROOT}/licenses/base"

while IFS='|' read -r entry destination; do
    [ -n "${entry}" ] || continue
    extract_one \
        "${OUTFIT_ZIP}" \
        "${OUTFIT_PREFIX}/${entry}" \
        "${SELECTED_ROOT}/${destination}"
done <<'OUTFIT_FILES'
Exports/FBX (Unity)/Modular Parts/Male_Peasant_Body.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Peasant_Feet.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Peasant_Legs.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Arms.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Body.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Feet_Boots.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Acc_Pauldron.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Head_Hood.fbx|outfit
Exports/FBX (Unity)/Modular Parts/Male_Ranger_Legs.fbx|outfit
Textures/Peasant/T_Peasant_2_BaseColor.png|textures
Textures/Ranger/T_Ranger_3_BaseColor.png|textures
License_Standard.txt|licenses/outfit
OUTFIT_FILES

mkdir -p "${RESOURCE_TEXTURE_ROOT}" "${LICENSE_ROOT}"
cp "${SELECTED_ROOT}/textures/T_Superhero_Male_Ligh.png" \
    "${RESOURCE_TEXTURE_ROOT}/Skin.png"
cp "${SELECTED_ROOT}/textures/T_Eye_Brown.png" \
    "${RESOURCE_TEXTURE_ROOT}/Eyes.png"
cp "${SELECTED_ROOT}/textures/T_Hair_2_BaseColor.png" \
    "${RESOURCE_TEXTURE_ROOT}/Hair.png"
cp "${SELECTED_ROOT}/textures/T_Peasant_2_BaseColor.png" \
    "${RESOURCE_TEXTURE_ROOT}/Robe.png"
cp "${SELECTED_ROOT}/textures/T_Ranger_3_BaseColor.png" \
    "${RESOURCE_TEXTURE_ROOT}/Ranger.png"
cp "${SELECTED_ROOT}/licenses/base/License_Standard.txt" \
    "${LICENSE_ROOT}/Quaternius_Universal_Base_Characters_CC0.txt"
cp "${SELECTED_ROOT}/licenses/outfit/License_Standard.txt" \
    "${LICENSE_ROOT}/Quaternius_Modular_Character_Outfits_Fantasy_CC0.txt"

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
        hash="$(sha256_file "${file}")"
        relative="${file#${PROJECT_ROOT}/}"
        printf '%s  %s\n' "${hash}" "${relative}"
    done > "${temporary_hash_file}"
mv "${temporary_hash_file}" "${HASH_FILE}"

printf 'Selected character sources ready: %s\n' "${SELECTED_ROOT}"
find "${SELECTED_ROOT}" -type f -print | LC_ALL=C sort
printf 'Unity character textures ready: %s\n' "${RESOURCE_TEXTURE_ROOT}"
printf 'Integrity manifest written: %s\n' "${HASH_FILE}"
