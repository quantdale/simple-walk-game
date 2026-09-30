using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// F-01 regression coverage: a MISSING primary save slot must recover the healthy
/// backup generation instead of being reported as "no save found".
///
/// The defect this file closes was total loss of a player's world and it survived a
/// hardening campaign that specifically covered crash-during-promotion, because the
/// store layer had a test for the exact on-disk state and the APPLICATION layer had
/// none. Every test here drives <see cref="GameSession.Continue"/> against that state.
/// </summary>
public sealed class SessionMissingPrimaryRecoveryTests : IDisposable
{
    private const string PrimaryFile = "save.json";
    private const string BackupFile = "save.backup.json";
    private const string TempFile = "save.tmp";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ------------------------------------------------------------------
    // 3.1 The defect itself: primary deleted, stale temp present, healthy backup.
    // ------------------------------------------------------------------

    [Fact]
    public void MissingPrimary_WithHealthyBackup_RecoversInsteadOfReportingNoSave()
    {
        byte[] backupBytes = BuildWorldWithBackup();

        // Exactly the state AtomicFileSaveStore.ReplaceFile's own comment documents:
        // "a crash between the two steps leaves the previous generation in backup", so the
        // primary is transiently absent and a leftover temp file may remain.
        File.Delete(_temp.FilePath(PrimaryFile));
        File.WriteAllText(_temp.FilePath(TempFile), "half-written garbage from a crashed commit");

        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        var result = session.Continue();

        Assert.Equal(StartStatus.RecoveredFromBackup, result.Status);
        Assert.True(session.HasLoadedState);

        // The recovered world is the BACKUP's world, not a fresh one.
        var backupState = TestSessions.NewCodec().Decode(backupBytes);
        Assert.Equal(CodecStatus.Ok, backupState.Status);
        long expectedVitality = backupState.State!.Resources.Get(ResourceType.Vitality);
        var home = session.GetHome();
        Assert.Equal(expectedVitality, home.Vitality);
        Assert.True(home.CompletedProjects > 0, "the recovered world must not be a fresh empty one");

        // The crashed temp file is never promoted to either slot as data: what lands in the
        // primary slot is the recovered BACKUP world, not the leftover temp bytes.
        var reloaded = TestSessions.NewCodec().Decode(File.ReadAllBytes(_temp.FilePath(PrimaryFile)));
        Assert.Equal(CodecStatus.Ok, reloaded.Status);
        Assert.Equal(expectedVitality, reloaded.State!.Resources.Get(ResourceType.Vitality));
    }

    [Fact]
    public void MissingPrimary_RecoveryNamesTheMissingPrimaryCauseInDiagnostics()
    {
        BuildWorldWithBackup();
        File.Delete(_temp.FilePath(PrimaryFile));

        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        Assert.Equal(StartStatus.RecoveredFromBackup, session.Continue().Status);

        var diag = session.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.RecoveredFromMissingPrimary, diag.BootOutcome);
        Assert.True(diag.RecoveredFromBackup);
        Assert.Equal(SaveRecoveryCause.PrimaryMissing, diag.BootRecoveryCause);
    }

    // ------------------------------------------------------------------
    // 3.2 Both generations absent really is a fresh profile, and writes nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void BothGenerationsAbsent_ReportsNoSaveFound_AndWritesNothing()
    {
        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));

        var result = session.Continue();

        Assert.Equal(StartStatus.NoSaveFound, result.Status);
        Assert.Equal(DiagnosticsBootOutcome.NoSaveFound, session.GetDiagnostics().BootOutcome);
        Assert.False(File.Exists(_temp.FilePath(PrimaryFile)));
        Assert.False(File.Exists(_temp.FilePath(BackupFile)));
    }

    // ------------------------------------------------------------------
    // 3.3 Absent primary + undecodable backup: fail closed, name the real reason,
    //     and overwrite neither slot.
    // ------------------------------------------------------------------

    [Fact]
    public void MissingPrimary_WithCorruptBackup_FailsClosedWithTheBackupReason()
    {
        BuildWorldWithBackup();
        File.Delete(_temp.FilePath(PrimaryFile));

        byte[] damagedBackup = CorruptEnvelope(_temp.FilePath(BackupFile));
        Assert.NotEmpty(damagedBackup);

        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        var result = session.Continue();

        // A corrupt file is a FILE failure, not an invariant failure.
        Assert.Equal(StartStatus.SaveUnreadable, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Detail));
        Assert.Equal(DiagnosticsBootOutcome.SaveUnreadable, session.GetDiagnostics().BootOutcome);

        // Fail closed: neither slot is overwritten and no new world appears.
        Assert.False(File.Exists(_temp.FilePath(PrimaryFile)));
        Assert.True(damagedBackup.AsSpan().SequenceEqual(File.ReadAllBytes(_temp.FilePath(BackupFile))));
    }

    // ------------------------------------------------------------------
    // 3.4 Absent primary + backup that decodes but violates an invariant:
    //     its own class, with the violations in the reason.
    // ------------------------------------------------------------------

    [Fact]
    public void MissingPrimary_WithStateInvalidBackup_ReportsStateInvalidWithViolations()
    {
        byte[] backupBytes = BuildWorldWithBackup();
        File.Delete(_temp.FilePath(PrimaryFile));

        // A CORRECTLY framed and checksummed payload carrying a content id the authored
        // region does not define. It decodes cleanly, so the only failure is validation.
        var payload = PayloadSurgery.OpenPayload(backupBytes);
        payload["region"]!["projects"]!["proj.ghost-from-the-future"] = new JsonObject
        {
            ["projectId"] = "proj.ghost-from-the-future",
            ["status"] = "completed",
            ["vitalityInvested"] = 10L,
            ["completedAtUtc"] = TestSessions.T0.AddDays(1),
        };
        File.WriteAllBytes(_temp.FilePath(BackupFile),
            PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, TestSessions.T0.AddDays(1)));

        var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        var result = session.Continue();

        Assert.Equal(StartStatus.StateInvalid, result.Status);
        Assert.Contains("validation", result.Detail!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DiagnosticsBootOutcome.StateInvalid, session.GetDiagnostics().BootOutcome);
        Assert.Equal(
            CodecFailureCategory.StateValidationFailed,
            session.GetDiagnostics().LastBootCodecFailure);
    }

    // ------------------------------------------------------------------
    // 3.5 The cause distinguishes missing-primary from damaged-primary recovery.
    // ------------------------------------------------------------------

    [Fact]
    public void RecoveryCause_DistinguishesMissingPrimaryFromDamagedPrimary()
    {
        // Damaged primary (present but unreadable), healthy backup.
        BuildWorldWithBackup();
        byte[] goodBackup = File.ReadAllBytes(_temp.FilePath(BackupFile));
        CorruptEnvelope(_temp.FilePath(PrimaryFile));

        var damagedPrimarySession = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        Assert.Equal(StartStatus.RecoveredFromBackup, damagedPrimarySession.Continue().Status);
        var damagedDiag = damagedPrimarySession.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.RecoveredFromBackup, damagedDiag.BootOutcome);
        Assert.Equal(SaveRecoveryCause.PrimaryDamaged, damagedDiag.BootRecoveryCause);

        // Now the absent-primary case over the same healthy backup.
        File.Delete(_temp.FilePath(PrimaryFile));
        File.WriteAllBytes(_temp.FilePath(BackupFile), goodBackup);

        var missingPrimarySession = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        Assert.Equal(StartStatus.RecoveredFromBackup, missingPrimarySession.Continue().Status);
        var missingDiag = missingPrimarySession.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.RecoveredFromMissingPrimary, missingDiag.BootOutcome);
        Assert.Equal(SaveRecoveryCause.PrimaryMissing, missingDiag.BootRecoveryCause);
    }

    // ------------------------------------------------------------------
    // 3.6 Repeated boots are stable: same generation, no accumulating notices.
    // ------------------------------------------------------------------

    [Fact]
    public void RepeatedMissingPrimaryBoots_RecoverTheSameGeneration_WithoutAccumulatingNotices()
    {
        BuildWorldWithBackup();
        File.Delete(_temp.FilePath(PrimaryFile));

        string? firstNotice = null;
        for (int boot = 0; boot < 3; boot++)
        {
            // Each iteration observes the SAME absent-primary condition, which is what
            // "booted repeatedly" means for this profile: the recovery commit re-creates
            // the primary, so a boot that did NOT re-delete it would legitimately be a
            // normal Loaded boot rather than a second recovery.
            File.Delete(_temp.FilePath(PrimaryFile));
            var session = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2 + boot)));
            var result = session.Continue();
            Assert.Equal(StartStatus.RecoveredFromBackup, result.Status);
            Assert.Equal(DiagnosticsBootOutcome.RecoveredFromMissingPrimary,
                session.GetDiagnostics().BootOutcome);

            var notice = result.SummaryLines.Single(l => l.Contains("backup", StringComparison.OrdinalIgnoreCase));
            if (firstNotice == null)
                firstNotice = notice;
            else
                Assert.Equal(firstNotice, notice);

            // Bounded: the summary never grows without limit across boots.
            Assert.True(result.SummaryLines.Count <= 12,
                "return summary must stay inside the 12-item bound across repeated recovery boots");
        }
    }

    // ------------------------------------------------------------------
    // 3.7 The healthy backup is never orphaned; a later normal commit rotates once.
    // ------------------------------------------------------------------

    [Fact]
    public void AfterMissingPrimaryBoot_BackupSurvives_AndANormalCommitRotatesItExactlyOnce()
    {
        BuildWorldWithBackup();
        byte[] recoveredGeneration = File.ReadAllBytes(_temp.FilePath(BackupFile));
        File.Delete(_temp.FilePath(PrimaryFile));

        // Recovery boot: primary is re-created, backup is left byte-identical because the
        // recovery commit must not rotate an absent primary into the backup slot.
        var recovering = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(2)));
        Assert.Equal(StartStatus.RecoveredFromBackup, recovering.Continue().Status);

        Assert.True(File.Exists(_temp.FilePath(PrimaryFile)));
        Assert.True(recoveredGeneration.AsSpan().SequenceEqual(File.ReadAllBytes(_temp.FilePath(BackupFile))),
            "the recovery commit must not overwrite the generation it recovered from");

        // A subsequent NORMAL commit rotates the (now healthy) primary into the backup,
        // exactly once, replacing the older recovered generation.
        var normal = TestSessions.Create(_temp, new ManualClock(TestSessions.T0.AddDays(3)));
        Assert.Equal(StartStatus.Loaded, normal.Continue().Status);
        normal.EnqueueProject(TestSessions.EntryProjectId);

        var codec = TestSessions.NewCodec();
        var rotatedBackup = codec.Decode(File.ReadAllBytes(_temp.FilePath(BackupFile)));
        var currentPrimary = codec.Decode(File.ReadAllBytes(_temp.FilePath(PrimaryFile)));
        Assert.Equal(CodecStatus.Ok, rotatedBackup.Status);
        Assert.Equal(CodecStatus.Ok, currentPrimary.Status);

        // Exactly one rotation happened: the backup slot now holds the generation the
        // normal boot loaded. Compared at the BYTE level, because this world's Vitality
        // balance is coincidentally the same on both generations and would not
        // discriminate a rotation from no rotation at all.
        Assert.Equal(
            currentPrimary.State!.Resources.Get(ResourceType.Vitality),
            rotatedBackup.State!.Resources.Get(ResourceType.Vitality));
        Assert.False(recoveredGeneration.AsSpan().SequenceEqual(File.ReadAllBytes(_temp.FilePath(BackupFile))),
            "a normal commit must rotate the primary into the backup, replacing the older generation");
    }

    /// <summary>
    /// Breaks the envelope's integrity so the file really is undecodable. Flipping a byte
    /// at offset 0 is NOT damage here: the envelope is JSON and already starts with '{'.
    /// Rewriting the base64 payload leaves a well-framed document whose checksum no longer
    /// matches, which is the realistic corruption class.
    /// </summary>
    private byte[] CorruptEnvelope(string path)
    {
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        string payload = envelope["payloadBase64"]!.GetValue<string>();
        // Change one payload character: framing stays valid, the integrity check fails.
        char replacement = payload[0] == 'A' ? 'B' : 'A';
        envelope["payloadBase64"] = replacement + payload.Substring(1);
        byte[] damaged = JsonSerializer.SerializeToUtf8Bytes(envelope);
        File.WriteAllBytes(path, damaged);
        return File.ReadAllBytes(path);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a real mid-game world with BOTH generations populated, by committing twice
    /// (the store rotates the previous primary into the backup on each normal commit).
    /// Returns the backup generation's bytes.
    /// </summary>
    private byte[] BuildWorldWithBackup()
    {
        var clock = new ManualClock(TestSessions.T0);
        var first = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.NewGameCreated, first.StartNewGame(seed: 7UL).Status);

        first.EnqueueProject(TestSessions.EntryProjectId);
        first.ActivateQueuedProject(TestSessions.EntryProjectId);
        first.CreditActivity(TestSessions.Tx1, TestSessions.T0.AddHours(1), 500L, "seed world");

        // Second commit: this rotation creates a healthy backup generation.
        first.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddHours(2), 300L, "advance world");

        Assert.True(File.Exists(_temp.FilePath(PrimaryFile)));
        Assert.True(File.Exists(_temp.FilePath(BackupFile)));
        return File.ReadAllBytes(_temp.FilePath(BackupFile));
    }
}
