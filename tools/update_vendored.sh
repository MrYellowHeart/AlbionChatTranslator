#!/usr/bin/env bash
# Refreshes external/StatisticsAnalysis from upstream.
# Usage: tools/update_vendored.sh [commit-ish]   (defaults to upstream main)
set -euo pipefail
REF="${1:-main}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$ROOT/external/StatisticsAnalysis"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

git -c core.longpaths=true clone -q --filter=blob:none --no-checkout \
  https://github.com/Triky313/AlbionOnline-StatisticsAnalysis "$TMP/upstream"
cd "$TMP/upstream"
COMMIT="$(git rev-parse "origin/$REF" 2>/dev/null || git rev-parse "$REF")"

for proj in Abstractions Diagnostics PhotonPackageParser Protocol18 Network; do
  rm -rf "$DEST/StatisticsAnalysisTool.$proj"
  git archive "$COMMIT" "src/StatisticsAnalysisTool.$proj" | tar -x -C "$TMP"
  mv "$TMP/src/StatisticsAnalysisTool.$proj" "$DEST/"
done
git show "$COMMIT:src/StatisticsAnalysisTool/Network/EventCodes.cs" > "$DEST/StatisticsAnalysisTool.Network/EventCodes.cs"
git show "$COMMIT:LICENSE" > "$DEST/LICENSE"
sed -i.bak -E "s/commit \`[0-9a-f]{40}\`[^,]*/commit \`$COMMIT\`/" "$DEST/VENDORED.md" && rm -f "$DEST/VENDORED.md.bak"

echo "Vendored upstream $COMMIT. Now run: dotnet test"
