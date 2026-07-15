#!/usr/bin/env bash

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GOALS_FILE="${PROJECT_ROOT}/docs/10_GOALS.md"

usage() {
    printf 'Usage: %s [--list | --current | --next | --show GOAL_ID]\n' "$0"
}

list_goals() {
    awk '
        /^### G/ { heading = $0; sub(/^### /, "", heading) }
        /^```yaml$/ { in_yaml = 1; id = phase = status = deps = ""; next }
        /^```$/ && in_yaml {
            if (deps == "") deps = "-"
            if (id != "") print id "\t" phase "\t" status "\t" deps "\t" heading
            in_yaml = 0
            next
        }
        in_yaml && /^id:/ { id = $2 }
        in_yaml && /^phase:/ { phase = $2 }
        in_yaml && /^status:/ { status = $2 }
        in_yaml && /^depends_on:/ {
            deps = $0
            sub(/^depends_on: \[/, "", deps)
            sub(/\]$/, "", deps)
        }
    ' "${GOALS_FILE}"
}

show_goal() {
    local wanted="$1"
    awk -v wanted="${wanted}" '
        $0 ~ "^### " wanted "( |$)" { found = 1 }
        found && $0 ~ /^### G/ && $0 !~ "^### " wanted "( |$)" { exit }
        found { print }
        END { if (!found) exit 3 }
    ' "${GOALS_FILE}"
}

current_id() {
    list_goals | awk -F '\t' '$3 == "in_progress" { print $1 }'
}

next_id() {
    list_goals | awk -F '\t' '
        {
            count++
            order[count] = $1
            status[$1] = $3
            deps[$1] = $4
        }
        $3 == "in_progress" { active = $1 }
        END {
            for (i = 1; i <= count; i++) {
                id = order[i]
                if (status[id] != "pending") continue
                ready = 1
                dependency_count = split(deps[id], dependency, /, */)
                for (j = 1; j <= dependency_count; j++) {
                    required = dependency[j]
                    if (required == "" || required == "-") continue
                    if (required == active) continue
                    if (status[required] != "done" && status[required] != "implemented") {
                        ready = 0
                        break
                    }
                }
                if (ready) {
                    print id
                    exit
                }
            }
        }
    '
}

case "${1:---current}" in
    --list)
        printf 'GOAL\tPHASE\tSTATUS\tDEPENDS_ON\tNAME\n'
        list_goals
        ;;
    --current)
        active="$(current_id)"
        if [[ -z "${active}" ]]; then
            printf 'No in_progress Goal. Run %s --next.\n' "$0" >&2
            exit 2
        fi
        show_goal "${active}"
        ;;
    --next)
        next="$(next_id)"
        if [[ -z "${next}" ]]; then
            printf 'No dependency-ready pending Goal.\n'
            exit 2
        fi
        show_goal "${next}"
        ;;
    --show)
        [[ $# == 2 ]] || { usage >&2; exit 2; }
        show_goal "$2" || {
            printf 'Unknown Goal: %s\n' "$2" >&2
            exit 3
        }
        ;;
    -h|--help)
        usage
        ;;
    *)
        usage >&2
        exit 2
        ;;
esac
