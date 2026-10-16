#!/usr/bin/env bash
# Editor-rendered (NOT device) screen tour: one PNG per ShellScreen via capture-valley.sh.
# Usage: tools/unity/capture-screen-tour.sh [out_dir]   (default docs/evidence/screen-tour)
# Each screen is a separate Unity run (~1-2 min each); run detached (nohup ... &).
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
out="$(realpath -m "${1:-$root/docs/evidence/screen-tour}")"; mkdir -p "$out"
screens="Onboarding Guide Home ReturnSummary Projects Region Story Discoveries Progress Settings ActivityStatus DeleteConfirm BootError Celebration"
summary="$out/TOUR_RUN.txt"; : > "$summary"
for s in $screens; do
  name="$(echo "$s" | tr 'A-Z' 'a-z')"
  WG_CAPTURE_SCREEN="$s" "$root/tools/unity/capture-valley.sh" "$out/$name-editor-rendered.png" >"$out/.last.txt" 2>&1
  rc=$?
  line="$(grep -h 'WG_CAPTURE' "$out/.last.txt" | tail -1)"
  echo "$s rc=$rc ${line}" | tee -a "$summary"
  mv -f "$out/capture-run.log" "$out/$name-capture-run.log" 2>/dev/null
  mv -f "$out/capture-results.xml" "$out/$name-capture-results.xml" 2>/dev/null
done
rm -f "$out/.last.txt"
echo "TOUR DONE" | tee -a "$summary"
