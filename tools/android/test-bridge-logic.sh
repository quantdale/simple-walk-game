#!/usr/bin/env bash
# Headless tests for the Android-free parts of the Health Connect bridge (KI-016(a)).
# Uses javac/java from PATH, then JAVA_HOME, then Unity's bundled OpenJDK.
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
src="$root/src/WalkGame.Unity/Assets/Plugins/Android/WalkGameHealth.androidlib/src/main/java/com/quantdale/walkgame/health"
bin=""
for c in "$(command -v javac 2>/dev/null || true)" "${JAVA_HOME:-}/bin/javac" \
         "$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/javac"; do
  if [ -n "$c" ] && [ -x "$c" ]; then bin="$(dirname "$c")"; break; fi
done
[ -n "$bin" ] || { echo "test-bridge-logic: no JDK found" >&2; exit 2; }
out="$(mktemp -d)"; trap 'rm -rf "$out"' EXIT
"$bin/javac" -d "$out" "$src/GapReconciler.java" "$root/tools/android/tests/com/quantdale/walkgame/health/GapReconcilerTest.java"
"$bin/java" -cp "$out" com.quantdale.walkgame.health.GapReconcilerTest
# D-123: reminder time maths and restart decision (seeded property/fuzz test).
rsrc="$root/src/WalkGame.Unity/Assets/Plugins/Android/WalkGameHealth.androidlib/src/main/java/com/quantdale/walkgame/reminder"
"$bin/javac" -d "$out" "$rsrc/ReminderTime.java" "$root/tools/android/tests/com/quantdale/walkgame/reminder/ReminderTimeTest.java"
"$bin/java" -cp "$out" com.quantdale.walkgame.reminder.ReminderTimeTest
