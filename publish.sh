#!/usr/bin/env bash
#
# Publishes the RelicStats mod to the Steam Workshop via steamcmd.
#
# One-time setup:
#   1. Fill in APP_ID below (from the STS2 store page URL:
#      https://store.steampowered.com/app/<APP_ID>/...).
#   2. Set STEAM_USER (env var or edit below) to a Steam account that owns STS2
#      and is allowed to edit the Workshop item. First run will prompt for the
#      password + Steam Guard code interactively; steamcmd caches the session
#      afterward so later runs are non-interactive.
#   3. First publish: leave PUBLISHED_FILE_ID empty. steamcmd creates a new item
#      and prints its ID — paste that back into PUBLISHED_FILE_ID for all future runs.
#   4. Set the item's *supported game versions* once on the Steamworks partner
#      site (steamcmd doesn't set them; they persist across content updates).
#
# Usage:
#   ./publish.sh "optional change note"
#
set -euo pipefail

# ── Config ─────────────────────────────────────────────────────────────
APP_ID="2868840"                       
PUBLISHED_FILE_ID="${PUBLISHED_FILE_ID:-}" # leave empty; auto-created & saved on first run
STEAM_USER="${STEAM_USER:-}"               # TODO: Steam builder account (required)
VISIBILITY="${VISIBILITY:-2}"              # 0=public 1=friends 2=private 3=unlisted
DOTNET="${DOTNET:-, dotnet}"                 # override if dotnet isn't on PATH
PREVIEW="${PREVIEW:-workshop_preview.png}" # optional; used only if the file exists
# ───────────────────────────────────────────────────────────────────────

cd "$(dirname "$0")"
REPO="$(pwd)"

err() { echo "error: $*" >&2; exit 1; }

[[ -n "$APP_ID" ]] || err "APP_ID is not set (edit publish.sh or export APP_ID=...)."
[[ -n "$STEAM_USER" ]] || err "STEAM_USER is not set (export STEAM_USER=your_builder_account)."

# Locate steamcmd
STEAMCMD="$(command -v steamcmd || true)"
[[ -n "$STEAMCMD" ]] || err "steamcmd not found on PATH. Install it (macOS: 'brew install --cask steamcmd', or grab the tarball from Valve) and retry."

CHANGENOTE="${1:-Update $(date -u +%Y-%m-%dT%H:%M:%SZ)}"

# Resolve the item ID: explicit env/config wins, else the saved .workshop_id file,
# else empty (steamcmd creates a new item and we save the ID it returns).
ID_FILE="$REPO/.workshop_id"
if [[ -z "$PUBLISHED_FILE_ID" && -f "$ID_FILE" ]]; then
  PUBLISHED_FILE_ID="$(tr -dc '0-9' < "$ID_FILE")"
  echo ">> Using saved Workshop item ID: $PUBLISHED_FILE_ID"
fi
CREATING=false
[[ -z "$PUBLISHED_FILE_ID" ]] && CREATING=true

# ── 1. Build (Release, against the pinned reference assemblies) ─────────
echo ">> Building RelicStats (Release)..."
"$DOTNET" build -c Release "$REPO/RelicStats.csproj"

# Godot.NET.Sdk emits to .godot/mono/temp/bin/<Config>/, not the default bin/ path.
DLL=""
for cand in \
  "$REPO/.godot/mono/temp/bin/Release/RelicStats.dll" \
  "$REPO/bin/Release/net9.0/RelicStats.dll"; do
  [[ -f "$cand" ]] && { DLL="$cand"; break; }
done
[[ -n "$DLL" ]] || DLL="$(find "$REPO/.godot" "$REPO/bin" -name RelicStats.dll -path '*Release*' 2>/dev/null | head -n1)"
[[ -n "$DLL" && -f "$DLL" ]] || err "built RelicStats.dll not found (looked in .godot/mono/temp/bin/Release and bin/Release/net9.0)."

JSON="$REPO/RelicStats.json"
[[ -f "$JSON" ]] || err "RelicStats.json not found at $JSON"

# Sanity: mod must not reference beta-only relic types unless intended.
# (Left as a reminder; harmless if you're on the reflection/single-DLL plan.)

VERSION="$(grep -oE '"version"[[:space:]]*:[[:space:]]*"[^"]+"' "$JSON" | sed -E 's/.*"version".*"(.*)"/\1/')"
echo ">> Mod version: ${VERSION:-unknown}"

# ── 2. Stage the Workshop content folder (flat: dll + json) ────────────
CONTENT="$REPO/build/workshop-content"
rm -rf "$CONTENT"
mkdir -p "$CONTENT"
cp "$DLL" "$CONTENT/"
cp "$JSON" "$CONTENT/"
echo ">> Staged content in $CONTENT:"
ls -1 "$CONTENT"

# ── 3. Generate the workshop_item.vdf manifest ─────────────────────────
VDF="$REPO/build/workshop_item.vdf"
{
  echo '"workshopitem"'
  echo '{'
  echo "    \"appid\"           \"$APP_ID\""
  echo "    \"publishedfileid\" \"${PUBLISHED_FILE_ID:-0}\""
  echo "    \"contentfolder\"   \"$CONTENT\""
  echo "    \"visibility\"      \"$VISIBILITY\""
  echo "    \"title\"           \"Relic Stats\""
  echo "    \"changenote\"      \"$CHANGENOTE\""
  if [[ -f "$REPO/$PREVIEW" ]]; then
    echo "    \"previewfile\"     \"$REPO/$PREVIEW\""
  fi
  echo '}'
} > "$VDF"
echo ">> Wrote manifest $VDF"

if $CREATING; then
  echo ">> No item ID yet — steamcmd will CREATE a new Workshop item and the ID"
  echo ">> will be saved to $ID_FILE automatically."
fi

# ── 4. Upload via steamcmd (tee output so we can capture a new item ID) ─
echo ">> Uploading via steamcmd (first run will prompt for password + Steam Guard)..."
LOG="$REPO/build/steamcmd.out"
set +e
"$STEAMCMD" +login "$STEAM_USER" +workshop_build_item "$VDF" +quit 2>&1 | tee "$LOG"
STATUS=${PIPESTATUS[0]}
set -e
[[ $STATUS -eq 0 ]] || err "steamcmd exited with status $STATUS (see $LOG)."

# ── 5. On first publish, capture & save the new item ID ────────────────
if $CREATING; then
  NEW_ID="$(grep -oiE 'PublishedFileID[^0-9]*[0-9]+' "$LOG" | grep -oE '[0-9]+' | tail -1)"
  if [[ -n "$NEW_ID" ]]; then
    echo "$NEW_ID" > "$ID_FILE"
    echo ">> Created Workshop item $NEW_ID (saved to $ID_FILE)."
    echo ">> Workshop URL: https://steamcommunity.com/sharedfiles/filedetails/?id=$NEW_ID"
    echo ">> NOTE: new items default to the visibility set above (VISIBILITY=$VISIBILITY);"
    echo ">>       set the supported game-version range once on the Steamworks partner site."
  else
    echo ">> Could not parse the new item ID from steamcmd output ($LOG)."
    echo ">> Find it in the output/your Workshop page and write it to $ID_FILE."
  fi
else
  echo ">> Updated Workshop item $PUBLISHED_FILE_ID."
  echo ">> Workshop URL: https://steamcommunity.com/sharedfiles/filedetails/?id=$PUBLISHED_FILE_ID"
fi
echo ">> Done."
