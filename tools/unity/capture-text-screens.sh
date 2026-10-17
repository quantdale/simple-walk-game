#!/usr/bin/env bash
# Editor-rendered (NOT device) PNGs of the Region 1 text screens via capture-valley.sh (D-148):
# Home and the return summary showing the D-145 first-session payoff line, Settings at 1x, and Home at 2x text.
# Usage: tools/unity/capture-text-screens.sh [out_dir]   (default docs/evidence/d148-text-screens)
# One Unity run per capture (~1-2 min each); run detached (nohup ... &). Only the PNGs and TEXT_RUN.txt are meant to be committed.
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
out="$(realpath -m "${1:-$root/docs/evidence/d148-text-screens}")"; mkdir -p "$out"
summary="$out/TEXT_RUN.txt"; : > "$summary"
# name|screen|state|text scale
captures="home-payoff|Home|payoff|1 returnsummary-payoff|ReturnSummary|payoff|1 settings|Settings|payoff|1 home-payoff-2x|Home|payoff|2"
for spec in $captures; do
  IFS='|' read -r name screen state scale <<<"$spec"
  WG_CAPTURE_SCREEN="$screen" WG_CAPTURE_STATE="$state" WG_CAPTURE_TEXT_SCALE="$scale" \
    "$root/tools/unity/capture-valley.sh" "$out/$name-editor-rendered.png" >"$out/.last.txt" 2>&1
  rc=$?
  echo "$name screen=$screen state=$state scale=$scale rc=$rc $(grep -h 'WG_CAPTURE device' "$out/.last.txt" | tail -1 | sed 's/ path=.*//')" | tee -a "$summary"
  rm -f "$out/capture-run.log" "$out/capture-results.xml"
done
rm -f "$out/.last.txt"
echo "TEXT CAPTURE DONE" | tee -a "$summary"
