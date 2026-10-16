#!/usr/bin/env bash
# PlayMode suite in batchmode -nographics (player loop runs; no GPU). Same contract as run-editmode-tests.sh.
TEST_PLATFORM=PlayMode exec "$(dirname "$0")/run-editmode-tests.sh" "$@"
