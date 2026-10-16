#!/usr/bin/env bash
# RETIRED by D-116. This script used to push a Unity iOS Xcode export as chunks to a throwaway
# ios-export/<tag> branch of THIS PUBLIC repository. That export contains Unity's proprietary
# engine library (Libraries/libiPhone-lib.a) and other Unity binaries; publishing them is a
# licence/redistribution risk, so the owner deleted ios-export/grok2251-a.
# Unity engine binaries must never be pushed to the public repo. A future verification needs a
# PRIVATE transport (see .github/workflows/ios-xcode-export-verify.yml and docs/HEALTHKIT_SETUP.md).
echo "push-ios-export.sh is retired (D-116): Unity engine binaries must never be pushed to the public repo." >&2
echo "Use a private transport; see docs/DECISIONS.md D-116." >&2
exit 3
