#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GOALS_FILE="${PROJECT_ROOT}/docs/10_GOALS.md"
PROGRESS_FILE="${PROJECT_ROOT}/docs/10_PROGRESS.md"
AGENTS_FILE="${PROJECT_ROOT}/AGENTS.md"
EVIDENCE_FILE="${PROJECT_ROOT}/docs/history/TEST_EVIDENCE.md"
GOAL_TOOL="${PROJECT_ROOT}/tools/goal_status.sh"

fail() {
    printf 'docs consistency failed: %s\n' "$1" >&2
    exit 1
}

for required in "${GOALS_FILE}" "${PROGRESS_FILE}" "${AGENTS_FILE}" "${EVIDENCE_FILE}" "${GOAL_TOOL}"; do
    [[ -f "${required}" ]] || fail "missing ${required#"${PROJECT_ROOT}/"}"
done

goal_rows="$({
    awk '
        /^```yaml$/ {
            in_yaml = 1
            id = phase = status = dependencies = ""
            has_refs = has_deliverables = has_acceptance = has_out_of_scope = 0
            next
        }
        /^```$/ && in_yaml {
            if (id != "") {
                if (dependencies == "") dependencies = "-"
                print id "\t" phase "\t" status "\t" dependencies "\t" \
                    has_refs "\t" has_deliverables "\t" has_acceptance "\t" has_out_of_scope
            }
            in_yaml = 0
            next
        }
        in_yaml && /^id:/ { id = $2 }
        in_yaml && /^phase:/ { phase = $2 }
        in_yaml && /^status:/ { status = $2 }
        in_yaml && /^depends_on:/ {
            dependencies = $0
            sub(/^depends_on: \[/, "", dependencies)
            sub(/\]$/, "", dependencies)
        }
        in_yaml && /^refs:/ { has_refs = 1 }
        in_yaml && /^deliverables:/ { has_deliverables = 1 }
        in_yaml && /^acceptance:/ { has_acceptance = 1 }
        in_yaml && /^out_of_scope:/ { has_out_of_scope = 1 }
    ' "${GOALS_FILE}"
})"

[[ -n "${goal_rows}" ]] || fail 'no Goal cards found'

duplicate_ids="$(printf '%s\n' "${goal_rows}" | awk -F '\t' '{print $1}' | sort | uniq -d)"
[[ -z "${duplicate_ids}" ]] || fail "duplicate Goal ids: ${duplicate_ids}"

missing_status="$(printf '%s\n' "${goal_rows}" | awk -F '\t' '$3 == "" {print $1}')"
[[ -z "${missing_status}" ]] || fail "Goal cards missing status: ${missing_status}"

invalid_status="$(printf '%s\n' "${goal_rows}" | awk -F '\t' \
    '$3 !~ /^(pending|in_progress|implemented|done|blocked)$/ {print $1 "=" $3}')"
[[ -z "${invalid_status}" ]] || fail "invalid Goal status: ${invalid_status}"

incomplete_cards="$(printf '%s\n' "${goal_rows}" | awk -F '\t' \
    '$2 == "" || $5 != 1 || $6 != 1 || $7 != 1 || $8 != 1 {print $1}')"
[[ -z "${incomplete_cards}" ]] || fail \
    "Goal cards missing phase/refs/deliverables/acceptance/out_of_scope: ${incomplete_cards}"

active_ids="$(printf '%s\n' "${goal_rows}" | awk -F '\t' '$3 == "in_progress" {print $1}')"
active_count="$(printf '%s\n' "${active_ids}" | awk 'NF {count++} END {print count + 0}')"
progress_id="$(awk -F'`' '$0 ~ /^\| 当前 Goal \|/ {print $2}' "${PROGRESS_FILE}")"
progress_status="$(awk -F'`' '$0 ~ /^\| 状态 \|/ {print $2; exit}' "${PROGRESS_FILE}")"
[[ -n "${progress_id}" ]] || fail '10_PROGRESS.md has no current Goal pointer'
if [[ "${active_count}" == "1" ]]; then
    [[ "${progress_id}" == "${active_ids}" ]] \
        || fail "progress points to ${progress_id}, card marks ${active_ids} in_progress"
    [[ "${progress_status}" == "in_progress" ]] \
        || fail "progress status is ${progress_status:-missing}, expected in_progress"
elif [[ "${active_count}" == "0" ]]; then
    unfinished_ids="$(printf '%s\n' "${goal_rows}" \
        | awk -F '\t' '$3 != "done" {print $1 "=" $3}')"
    [[ -z "${unfinished_ids}" ]] \
        || fail "no active Goal but unfinished cards remain: ${unfinished_ids}"
    progress_card_status="$(printf '%s\n' "${goal_rows}" \
        | awk -F '\t' -v wanted="${progress_id}" '$1 == wanted {print $3}')"
    [[ "${progress_card_status}" == "done" ]] \
        || fail "terminal progress points to ${progress_id}=${progress_card_status:-missing}"
    [[ "${progress_status}" == "done" ]] \
        || fail "terminal progress status is ${progress_status:-missing}, expected done"
else
    fail "expected at most one in_progress Goal, found ${active_count}: ${active_ids}"
fi

known_ids="$(printf '%s\n' "${goal_rows}" | awk -F '\t' '{print $1}')"
while IFS=$'\t' read -r id _phase _status dependencies _rest; do
    IFS=',' read -ra dependency_list <<< "${dependencies}"
    for dependency in "${dependency_list[@]}"; do
        dependency="${dependency// /}"
        [[ -z "${dependency}" || "${dependency}" == "-" ]] && continue
        [[ "${dependency}" != "${id}" ]] || fail "${id} depends on itself"
        printf '%s\n' "${known_ids}" | rg -qx "${dependency}" \
            || fail "${id} depends on unknown Goal ${dependency}"
        if [[ "${id}" == "${active_ids}" ]]; then
            dependency_status="$(printf '%s\n' "${goal_rows}" \
                | awk -F '\t' -v wanted="${dependency}" '$1 == wanted {print $3}')"
            [[ "${dependency_status}" == "implemented" || "${dependency_status}" == "done" ]] \
                || fail "active Goal ${id} has unsatisfied dependency ${dependency}=${dependency_status}"
        fi
    done
done <<< "${goal_rows}"

while read -r evidence_id; do
    [[ -n "${evidence_id}" ]] || continue
    validator="validate_$(printf '%s' "${evidence_id}" | tr '[:upper:]-' '[:lower:]_').sh"
    [[ -x "${PROJECT_ROOT}/tools/${validator}" ]] || fail "missing executable tools/${validator}"
done < <(awk -F'`' '$0 ~ /^\| `G/ {print $2}' "${EVIDENCE_FILE}")

if rg -q '当前仓库应恢复 `G[0-9]' "${AGENTS_FILE}"; then
    fail 'AGENTS.md must not hard-code a current Goal'
fi

next_output="$("${GOAL_TOOL}" --next 2>/dev/null || true)"
next_id="$(printf '%s\n' "${next_output}" | awk '/^id:/ {print $2; exit}')"
printf 'docs consistency passed: active=%s, next=%s, Goal cards complete and dependencies valid.\n' \
    "${active_ids:-none}" "${next_id:-none}"
