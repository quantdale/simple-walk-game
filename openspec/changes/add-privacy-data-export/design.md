# Design

## Layers
- Application: `GameSession.ExportSaveEnvelope()` returns `_codec.Encode(_state, _clock.UtcNow)` or null before a game is loaded. No JSON in Application.
- Infrastructure: `SaveExport.ToReadableJson(byte[] envelope)` parses the envelope, verifies the payload SHA-256, and writes the export document (indented, UTF-8). Returns a result with a status (`Exported`, `Malformed`, `ChecksumMismatch`); never throws on hostile bytes.
- Shell: `ShellController.ShareText` (`Func<string, bool>?`, wired by the bootstrap; null = unavailable). `export` builds the JSON, enforces `MaxExportChars = 512 * 1024`, calls the share function, and reports a calm line. `AndroidShareSheet.ShareText` uses `AndroidJavaObject`; exceptions are caught and reported as a failed share.

## Copy
Lines are plain sentences, pass the no-guilt copy scan (D-096), and contain no raw identifiers.
