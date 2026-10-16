# Add a local-only "Privacy and your data" screen with save export through the OS share sheet

**Status:** Approved for implementation by the executing agent (D-111). Region 1 / shell scope with no persisted-shape change; agent approval is allowed because the only gates on it (D-011, the ROADMAP post-MVP gate) concern Region 2 (precedent: D-103). No network code is added.

## Why

PRODUCT_SPEC §7a and PRIVACY_REVIEW say all data stays on the phone, and Settings already offers "Delete all local data". Players have no way to see, in plain words, what the game stores, and no way to take a copy of their own data. A data-export path is also the usual expectation for health-adjacent data (portability) and gives players a manual backup.

## What Changes

- New shell screen `Privacy` ("Privacy and your data"), reached from Settings. Plain-language lines:
  - what is stored (world progress; walking records: step counts, start and end times, the walking app's record ids, local dates; settings and onboarding progress);
  - where (only in this app's private storage on this phone, protected by the phone's encryption, D-067);
  - what is never collected (no name, email, location, accounts, ads, analytics; nothing is sent anywhere).
- Action `export`: builds a readable JSON export of the current save and hands it to the OS share sheet as text. The player chooses where it goes; the game itself makes no network call and writes no file to shared storage.
- Export document (Infrastructure `SaveExport`): `{ format: "simple-walk-game-export", formatVersion: 1, schemaVersion, savedAtUtc, payloadSha256, state: <the decoded save payload> }`. Built from `GameSession.ExportSaveEnvelope()` (the exact envelope the codec writes, so it reflects validated state). The checksum is verified before export; a mismatch exports nothing.
- Fail closed: no loaded game, no share sheet on this device, an export over 512 KB, or a share failure gives one calm sentence and changes nothing.
- Android share sheet: `Intent.ACTION_SEND`, `text/plain`, `EXTRA_TEXT`, via `Intent.createChooser` (C# `AndroidJavaObject`, no new Java/manifest/FileProvider). Other platforms report sharing unavailable.

## Non-goals

Import/restore from an export (a later change; it needs the save-trust rules). Exporting preferences (UX-only, D-042). Cloud backup, accounts, any network code. Writing export files to shared storage.

## Compatibility

No save schema change, no content change, no migration. A new `ShellScreen` value is appended last. Existing saves behave the same.

## Privacy

The export contains the health-adjacent data described in PRIVACY_REVIEW §3. It leaves the app only through the share target the player picks; the screen says so before the button. PRIVACY_REVIEW gets an export section.
