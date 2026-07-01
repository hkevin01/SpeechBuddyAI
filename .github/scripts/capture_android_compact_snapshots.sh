#!/usr/bin/env bash
set -euo pipefail

APP_ID="com.speechbuddy.ai"
SNAPSHOT_ROOT="tests/SpeechBuddyAI.Tests/UiSnapshots"
CURRENT_DIR="$SNAPSHOT_ROOT/current/compact-phone"
LAYOUT_DIR="$CURRENT_DIR/layout-bounds"

mkdir -p "$CURRENT_DIR" "$LAYOUT_DIR"
rm -f "$CURRENT_DIR"/*.png "$LAYOUT_DIR"/*.xml

echo "Building Android app for screenshot capture..."
dotnet build SpeechBuddyAI.csproj -f net9.0-android -c Release

APK_PATH=$(find . -type f \( -name "*Signed.apk" -o -name "*.apk" \) | grep "net9.0-android" | head -n 1)
if [[ -z "${APK_PATH:-}" ]]; then
  echo "Unable to locate built APK."
  exit 1
fi

echo "Installing APK: $APK_PATH"
adb uninstall "$APP_ID" >/dev/null 2>&1 || true
adb install -r "$APK_PATH"

echo "Launching app..."
adb shell monkey -p "$APP_ID" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1

read -r WIDTH HEIGHT <<< "$(adb shell wm size | sed -n 's/.* \([0-9]\+\)x\([0-9]\+\).*/\1 \2/p')"
if [[ -z "${WIDTH:-}" || -z "${HEIGHT:-}" ]]; then
  echo "Unable to determine emulator display size."
  exit 1
fi

TAB_Y=$((HEIGHT - 100))
TAB_COUNT=5
SCREENS=("home" "practice" "progress" "notes" "settings")

capture_current_screen() {
  local name="$1"
  adb exec-out screencap -p > "$CURRENT_DIR/${name}.png"
  adb shell uiautomator dump /sdcard/window_dump.xml >/dev/null 2>&1
  adb exec-out cat /sdcard/window_dump.xml > "$LAYOUT_DIR/${name}.xml"
}

for i in "${!SCREENS[@]}"; do
  x=$((WIDTH * (2 * i + 1) / (2 * TAB_COUNT)))
  adb shell input tap "$x" "$TAB_Y"
  capture_current_screen "${SCREENS[$i]}"
done

echo "Snapshot capture complete."
