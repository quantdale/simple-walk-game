## 1. Application
- [x] 1.1 `GameSession.ExportSaveEnvelope()` (tests: `tests/WalkGame.Application.Tests/SaveExportTests.cs`).
## 2. Infrastructure
- [x] 2.1 `SaveExport.ToReadableJson` with checksum verification (tests: `tests/WalkGame.Application.Tests/SaveExportTests.cs, which references Infrastructure`).
## 3. Unity
- [x] 3.1 `Privacy` screen, Settings entry, `export` action, `ShareText` seam, `AndroidShareSheet` (EditMode `PrivacyScreenTests`; accessibility audit covers the new screen).
## 4. Records
- [x] 4.1 D-111, PRIVACY_REVIEW export section, ROADMAP/KNOWN_ISSUES.
## 5. Verify
- [x] 5.1 `dotnet test` green; EditMode suite green; device share sheet UNVERIFIED (KI-002).
