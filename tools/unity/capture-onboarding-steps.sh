#!/usr/bin/env bash
# Editor-rendered (NOT device) PNGs of every onboarding step at 1x and 2x text via capture-valley.sh (D-164):
# the pre-start Onboarding screen, guide steps 1-5 and step 3 after a first project is chosen.
# At 2x each step is also captured scrolled to the end, so the primary button is shown.
# Usage: tools/unity/capture-onboarding-steps.sh [out_dir]   (default docs/evidence/d164-onboarding-steps)
# One Unity run per capture (~20-60 s each); run detached (nohup ... &). Commit the PNGs and ONBOARDING_RUN.txt.
set -uo pipefail
root="$(git rev-parse --show-toplevel)"
out="$(realpath -m "${1:-$root/docs/evidence/d164-onboarding-steps}")"; mkdir -p "$out"
summary="$out/ONBOARDING_RUN.txt"; : > "$summary"
# name|screen|guide step|chosen|scale|scroll
captures="step0-welcome|Onboarding|||1| step1-valley|Guide|1||1| step2-walking|Guide|2||1| step3-first-project|Guide|3||1| step3-chosen|Guide|3|1|1| step4-away|Guide|4||1| step5-done|Guide|5||1|"
for spec in $captures; do
  for scale in 1 2; do
    for scroll in top end; do
      [ "$scale" = 1 ] && [ "$scroll" = end ] && continue
      IFS='|' read -r name screen step chosen _ _ <<<"$spec"
      suffix=""; [ "$scale" = 2 ] && suffix="-2x"; [ "$scroll" = end ] && suffix="$suffix-end"
      png="$out/$name$suffix-editor-rendered.png"
      scrollenv=""; [ "$scroll" = end ] && scrollenv="end"
      WG_CAPTURE_SCREEN="$screen" WG_CAPTURE_GUIDE_STEP="$step" WG_CAPTURE_GUIDE_CHOSEN="$chosen" WG_CAPTURE_TEXT_SCALE="$scale" \
        WG_CAPTURE_SCROLL="$scrollenv" "$root/tools/unity/capture-valley.sh" "$png" >"$out/.last.txt" 2>&1
      rc=$?
      echo "$name$suffix screen=$screen step=${step:-0} chosen=${chosen:-0} scale=$scale scroll=$scroll rc=$rc $(grep -h 'WG_CAPTURE ' "$out/.last.txt" | grep device | tail -1 | sed 's/ path=.*//')" | tee -a "$summary"
      rm -f "$out/capture-run.log" "$out/capture-results.xml"
    done
  done
done
rm -f "$out/.last.txt"
echo "ONBOARDING CAPTURE DONE" | tee -a "$summary"
