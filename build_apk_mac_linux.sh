#!/usr/bin/env bash
# ============================================================================
# Project Real Loser v1.23 — One-Click Unity 2022.3.40f1 Android 9+ APK Builder
# ============================================================================
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY_BIN="${UNITY_EXECUTABLE:-}"

if [ -z "$UNITY_BIN" ]; then
  for candidate in     "/Applications/Unity/Hub/Editor/2022.3.40f1/Unity.app/Contents/MacOS/Unity"     "$HOME/Unity/Hub/Editor/2022.3.40f1/Editor/Unity"     "/opt/unity/Editor/Unity"     "$(command -v unity-editor 2>/dev/null || true)"; do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then
      UNITY_BIN="$candidate"
      break
    fi
  done
fi

if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
  echo "[ERROR] Unity 2022.3.40f1 Editor not found. Set UNITY_EXECUTABLE=/path/to/Unity and re-run."
  exit 1
fi

echo "[INFO] Building Android 9+ Universal APK using: $UNITY_BIN"
"$UNITY_BIN" -batchmode -nographics -quit   -projectPath "$PROJECT_DIR"   -executeMethod RealLoser.Editor.RealLoserProjectBuilder.BuildAndroidAPK   -logFile "$PROJECT_DIR/unity_android_build.log"

echo "[SUCCESS] APK generated at: $PROJECT_DIR/Builds/Android/ProjectRealLoser_v1.23_Android9.apk"
