#!/usr/bin/env bash
# scrub-check.sh — scan tracked files for private-host, LAN-IP, key-material, or
# home-path leakage before the repo goes public.
#
# Usage: bash scripts/scrub-check.sh
# Exit 0 = clean. Exit 1 = offences listed to stdout.

set -euo pipefail

OFFENCES=0

offence() {
  printf '%s:%s: %s\n' "$1" "$2" "$3"
  OFFENCES=1
}

# Enumerate tracked files, excluding this script itself.
FILES=()
while IFS= read -r f; do
  FILES+=("$f")
done < <(git ls-files | grep -vx 'scripts/scrub-check\.sh')

for f in "${FILES[@]}"; do
  lineno=0
  while IFS= read -r line; do
    lineno=$((lineno + 1))

    # 1. Absolute home-path: /home/<name>/ or /Users/<name>/
    if [[ "$line" =~ ^[^#]*\/home\/[^\/]+\/ || "$line" =~ ^[^#]*\/Users\/[^\/]+\/ ]]; then
      offence "$f" "$lineno" "absolute home path"
    fi

    # 2. Non-documentation IP (dotted quad not 127.0.0.1, 0.0.0.0, or 192.0.2.*)
    while [[ "$line" =~ ([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}) ]]; do
      ip="${BASH_REMATCH[1]}"
      line="${line#*"$ip"}"
      if [[ "$ip" != "127.0.0.1" && "$ip" != "0.0.0.0" && ! "$ip" =~ ^192\.0\.2\. ]]; then
        offence "$f" "$lineno" "non-documentation IP $ip"
        break
      fi
    done

    # 3. Private hostname: <label>.local or <label>.internal
    if [[ "$line" =~ ^[^#]*[[:alnum:]-]+\.[li][oa][cc][la][ll] ]]; then
      offence "$f" "$lineno" "private .local/.internal hostname"
    fi

    # 4. PEM private-key header
    if [[ "$line" =~ -----BEGIN\ [A-Z\ ]*PRIVATE\ KEY ]]; then
      offence "$f" "$lineno" "PEM private-key header"
    fi

    # 5. Secret-bearing names (case-insensitive): api_key, apikey, secret_key, password=
    local_lower="${line,,}"
    if [[ "$local_lower" =~ api[_]?key || "$local_lower" =~ secret[_]?key || "$local_lower" =~ password= ]]; then
      offence "$f" "$lineno" "secret-bearing name (api_key/apikey/secret_key/password=)"
    fi

  done < "$f"
done

if [[ "$OFFENCES" -eq 1 ]]; then
  printf '\n%d offence(s) found — commit the fixes and re-run.\n' "$OFFENCES"
  exit 1
fi

printf 'Clean — no offences detected.\n'
exit 0
