#!/usr/bin/env bash
#
# check-bom.sh — ensure UTF-8 BOM on C#/XAML source files so .NET Hot Reload
# can diff them reliably. Missing BOMs are the #1 cause of silent "my edit
# didn't apply" issues in MAUI Hot Reload.
#
# Usage:
#   ./scripts/check-bom.sh              # report files missing a BOM (exit 1 if any)
#   ./scripts/check-bom.sh --fix        # add a UTF-8 BOM to any file missing one
#   ./scripts/check-bom.sh --fix --verbose
#
# Scans: *.cs, *.xaml, *.xml, *.resx under src/ and tests/ (if present).
# Skips: bin/, obj/, node_modules/, .git/
#

set -euo pipefail

FIX=0
VERBOSE=0
for arg in "$@"; do
    case "$arg" in
        --fix) FIX=1 ;;
        --verbose|-v) VERBOSE=1 ;;
        --help|-h)
            sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *) echo "Unknown option: $arg" >&2; exit 2 ;;
    esac
done

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

ROOTS=()
[[ -d src   ]] && ROOTS+=(src)
[[ -d tests ]] && ROOTS+=(tests)
if [[ ${#ROOTS[@]} -eq 0 ]]; then
    echo "No src/ or tests/ directories under $REPO_ROOT" >&2
    exit 2
fi

# UTF-8 BOM bytes: EF BB BF
BOM=$'\xEF\xBB\xBF'

missing=()
fixed=()
scanned=0

while IFS= read -r -d '' f; do
    scanned=$((scanned + 1))
    head3=$(head -c 3 -- "$f" 2>/dev/null || true)
    if [[ "$head3" == "$BOM" ]]; then
        [[ $VERBOSE -eq 1 ]] && echo "  ok  $f"
        continue
    fi

    missing+=("$f")
    if [[ $FIX -eq 1 ]]; then
        tmp="$(mktemp "${f}.bom.XXXXXX")"
        printf '\xEF\xBB\xBF' > "$tmp"
        cat -- "$f" >> "$tmp"
        mode=$(stat -f '%Lp' "$f" 2>/dev/null || stat -c '%a' "$f" 2>/dev/null || echo "")
        [[ -n "$mode" ]] && chmod "$mode" "$tmp"
        mv -f -- "$tmp" "$f"
        fixed+=("$f")
        [[ $VERBOSE -eq 1 ]] && echo "  fix $f"
    fi
done < <(
    find "${ROOTS[@]}" \
        \( -path '*/bin' -o -path '*/obj' -o -path '*/node_modules' -o -path '*/.git' \) -prune -o \
        -type f \( -name '*.cs' -o -name '*.xaml' -o -name '*.xml' -o -name '*.resx' \) \
        -print0
)

echo
echo "Scanned $scanned file(s) under: ${ROOTS[*]}"
if [[ ${#missing[@]} -eq 0 ]]; then
    echo "✅  All files have a UTF-8 BOM. Hot Reload should be happy."
    exit 0
fi

if [[ $FIX -eq 1 ]]; then
    echo "🛠   Added BOM to ${#fixed[@]} file(s):"
    printf '    %s\n' "${fixed[@]}"
    echo
    echo "Review the diff:   git diff --stat"
    echo "Commit if desired: git add -A && git commit -m 'Add UTF-8 BOM to source files for Hot Reload'"
    exit 0
else
    echo "⚠️   ${#missing[@]} file(s) missing a UTF-8 BOM (Hot Reload may not pick up edits):"
    printf '    %s\n' "${missing[@]}"
    echo
    echo "Re-run with --fix to prepend a BOM to each:"
    echo "    $0 --fix"
    exit 1
fi
