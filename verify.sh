#!/usr/bin/env bash
# verify.sh — run every gate for Phase A, then lint README.md file references.
#
# Usage: bash verify.sh
# Exit 0 = all gates green. Exit 1 = first failure.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# --- Gates ---

echo "Gate 1/4: dotnet build -warnaserror"
dotnet build -warnaserror "$SCRIPT_DIR" >/dev/null 2>&1
echo "  OK"

echo "Gate 2/4: dotnet test"
dotnet test --no-build "$SCRIPT_DIR" >/dev/null 2>&1
echo "  OK"

echo "Gate 3/4: dotnet format --verify-no-changes"
dotnet format --verify-no-changes "$SCRIPT_DIR" >/dev/null 2>&1
echo "  OK"

echo "Gate 4/4: bash scripts/scrub-check.sh"
bash "$SCRIPT_DIR/scripts/scrub-check.sh"
echo "  OK"

# --- README-quickstart lint ---
# Extract every repo-relative path that appears in a fenced code block
# after --project, bash , or ./
README="$SCRIPT_DIR/README.md"
if [[ -f "$README" ]]; then
  # Grab lines inside fenced code blocks (``` … ```)
  IN_BLOCK=0
  while IFS= read -r line; do
    if [[ "$line" == '```'* ]]; then
      IN_BLOCK=$((1 - IN_BLOCK))
      continue
    fi
    [[ "$IN_BLOCK" -ne 1 ]] && continue

    # Collect paths matching: --project <path>, bash <path>, or ./<path>
    for match in $(echo "$line" | grep -oE '(--project |bash |\.\/)[^"'"'"' ]+' || true); do
      case "$match" in
        --project*) path="${match#--project}" ;;
        bash*)      path="${match#bash}" ;;
        ./*)        path="${match#./}" ;;
        *)          continue ;;
      esac
      # Strip trailing slash or quotes (defensive)
      path="${path%/}"
      path="${path%\"}"
      path="${path%\'}"
      if [[ ! -e "$SCRIPT_DIR/$path" ]]; then
        echo "FAIL: README.md references missing path: $path"
        exit 1
      fi
    done
  done < "$README"
fi

echo "PASS"
exit 0
