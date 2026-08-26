using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Tests.TestSupport;
using Xunit;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// M8-H1 persistence hostile-path evidence (campaign Workstream B).
///
/// Interruption scenarios are exercised by constructing the exact on-disk states a
/// crashed process leaves behind (stale temporaries, deleted primaries, partial
/// promotions, corrupted generations) rather than by mocking a filesystem — these
/// states are precisely what the failure windows produce, so the tests are faithful
/// and fully deterministic.
///
/// Responsibility split under test: the STORE owns bytes, atomicity and backups (any
/// non-empty file reads back successfully; content interpretation belongs to the
/// codec/session layer). The RECOVERY CONTRACT is that boot selects the newest valid
/// generation explicitly, recovery never destroys the last healthy generation, stale
/// temporary files never masquerade as canonical saves, and unrecoverable state fails
/// closed instead of being silently replaced.
/// </summary>
public sealed class PersistenceFaultInjectionTests
{
    private const string Slot = "save";
    private const string PrimaryName = Slot + ".json";
    private const string BackupName = Slot + ".backup.json";
    private const string TempName = Slot + ".tmp";
    private const string BackupTempName = Slot + ".backup.tmp";

    // ------------------------------------------------------------------
    // Recovery must not trade the last healthy generation for garbage.
    // ------------------------------------------------------------------

    [Fact]
    public void RecoveryRecommit_CorruptPrimary_HealthyGenerationSurvivesInBothSlots()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        var olderGeneration = Bytes(32);
        var newerGeneration = Bytes(48);
        store.WriteAtomic(olderGeneration);   // no previous primary → no backup yet
        store.WriteAtomic(newerGeneration);   // rotates older generation into backup

        CorruptFile(Path.Combine(temp.Path, PrimaryName));

        // Recovery source: the backup still holds the last healthy generation.
        var recoveryStore = new AtomicFileSaveStore(temp.Path);
        var recoveredBytes = recoveryStore.ReadBackup().EnvelopeBytes!;
        Assert.True(olderGeneration.SequenceEqual(recoveredBytes));

        // The recovery re-commit must NOT rotate the corrupt primary into the backup
        // slot: that would replace the last healthy generation with garbage.
        recoveryStore.WriteAtomicPreservingBackup(recoveredBytes);

        Assert.True(olderGeneration.SequenceEqual(
            File.ReadAllBytes(Path.Combine(temp.Path, PrimaryName))));
        Assert.True(olderGeneration.SequenceEqual(
            File.ReadAllBytes(Path.Combine(temp.Path, BackupName))));

        // Repeated recovery across multiple boots stays stable and healthy.
        var thirdStore = new AtomicFileSaveStore(temp.Path);
        Assert.Equal(SaveReadOutcome.Success, thirdStore.ReadPrimary().Outcome);
        Assert.Equal(SaveReadOutcome.Success, thirdStore.ReadBackup().Outcome);
    }

    [Fact]
    public void WriteAtomicPreservingBackup_NeverTouchesExistingBackup()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        var first = Bytes(24);
        var second = Bytes(40);
        store.WriteAtomic(first);
        store.WriteAtomic(second);

        var backupBefore = File.ReadAllBytes(Path.Combine(temp.Path, BackupName));
        Assert.True(first.SequenceEqual(backupBefore));

        var replacement = Bytes(64);
        store.WriteAtomicPreservingBackup(replacement);

        Assert.True(backupBefore.SequenceEqual(
            File.ReadAllBytes(Path.Combine(temp.Path, BackupName))));
        Assert.True(replacement.SequenceEqual(store.ReadPrimary().EnvelopeBytes!));
    }

    [Fact]
    public void FirstWriteEver_CreatesNoBackupSlot()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        store.WriteAtomic(Bytes(16));

        Assert.Equal(SaveReadOutcome.NotFound, store.ReadBackup().Outcome);
        Assert.Equal(SaveReadOutcome.Success, store.ReadPrimary().Outcome);
    }

    // ------------------------------------------------------------------
    // Interrupted promotion windows: exact post-crash disk states.
    // ------------------------------------------------------------------

    [Fact]
    public void CrashDuringPromotion_DeletedPrimary_StaleTempAndValidBackup_RecoverFromBackup()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        var gen1 = Bytes(40);
        var gen2 = Bytes(56);
        store.WriteAtomic(gen1);
        store.WriteAtomic(gen2);              // primary=gen2, backup=gen1

        // Reproduce a crash between File.Delete(primary) and File.Move(tmp → primary):
        // the new bytes are durable in the temp file, the primary slot is empty, and
        // the previous healthy generation sits in backup.
        File.WriteAllBytes(Path.Combine(temp.Path, TempName), Bytes(72));
        File.Delete(Path.Combine(temp.Path, PrimaryName));

        var afterCrash = new AtomicFileSaveStore(temp.Path);
        Assert.Equal(SaveReadOutcome.NotFound, afterCrash.ReadPrimary().Outcome);
        Assert.True(gen1.SequenceEqual(afterCrash.ReadBackup().EnvelopeBytes!),
            "the previous generation must remain recoverable from backup");

        // The stale temp is consumed by the next commit, never promoted as data itself.
        afterCrash.WriteAtomicPreservingBackup(gen2);

        Assert.False(File.Exists(Path.Combine(temp.Path, TempName)));
        Assert.True(gen2.SequenceEqual(File.ReadAllBytes(Path.Combine(temp.Path, PrimaryName))));
        Assert.True(gen1.SequenceEqual(File.ReadAllBytes(Path.Combine(temp.Path, BackupName))));
    }

    [Fact]
    public void StaleTemporaries_FromEarlierCrash_AreCleanedAtConstruction_AndNeverRead()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        store.WriteAtomic(Bytes(16));
        store.WriteAtomic(Bytes(24));
        var primaryBefore = File.ReadAllBytes(Path.Combine(temp.Path, PrimaryName));

        File.WriteAllText(Path.Combine(temp.Path, BackupTempName), "stale-backup-temp");
        File.WriteAllText(Path.Combine(temp.Path, TempName), "stale-primary-temp");

        var nextSession = new AtomicFileSaveStore(temp.Path);

        Assert.False(File.Exists(Path.Combine(temp.Path, BackupTempName)));
        Assert.False(File.Exists(Path.Combine(temp.Path, TempName)));
        Assert.Equal(SaveReadOutcome.Success, nextSession.ReadPrimary().Outcome);
        Assert.Equal(SaveReadOutcome.Success, nextSession.ReadBackup().Outcome);
        Assert.True(primaryBefore.SequenceEqual(nextSession.ReadPrimary().EnvelopeBytes!));
    }

    // ------------------------------------------------------------------
    // Malformed generations: the store never rewrites or deletes unreadable
    // saves behind the caller's back — only an explicit commit may.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("junk-not-json")]
    [InlineData("{\"schemaVersion\":2,\"savedAtUtc\":\"2026-01-01T00:00:00Z\",\"payloadSha256Base64\":\"AAAA\",\"payloadBase64\":\"!!!not-base64\"}")]
    [InlineData("")]
    public void UnreadableGenerations_AreNeverRewrittenOrDeletedByReadPaths(string malformedContent)
    {
        using var temp = new TempDirectory();
        WriteFile(temp.Path, PrimaryName, malformedContent);
        WriteFile(temp.Path, BackupName, malformedContent);

        var primaryBefore = File.ReadAllBytes(Path.Combine(temp.Path, PrimaryName));
        var backupBefore = File.ReadAllBytes(Path.Combine(temp.Path, BackupName));

        var store = new AtomicFileSaveStore(temp.Path);
        _ = store.ReadPrimary();
        _ = store.ReadBackup();

        Assert.True(primaryBefore.SequenceEqual(
            File.ReadAllBytes(Path.Combine(temp.Path, PrimaryName))));
        Assert.True(backupBefore.SequenceEqual(
            File.ReadAllBytes(Path.Combine(temp.Path, BackupName))));
    }

    [Fact]
    public void ValidPrimary_MalformedBackup_PrimaryRemainsAuthoritative()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        var gen1 = Bytes(48);
        var gen2 = Bytes(64);
        store.WriteAtomic(gen1);
        store.WriteAtomic(gen2);
        CorruptFile(Path.Combine(temp.Path, BackupName));

        var next = new AtomicFileSaveStore(temp.Path);
        Assert.Equal(SaveReadOutcome.Success, next.ReadPrimary().Outcome);
        Assert.True(gen2.SequenceEqual(next.ReadPrimary().EnvelopeBytes!));
        Assert.NotEqual(gen2, File.ReadAllBytes(Path.Combine(temp.Path, BackupName)));
    }

    [Fact]
    public void EmptyPrimary_ValidBackup_BackupRemainsTheRecoverableGeneration()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);
        var gen1 = Bytes(48);
        store.WriteAtomic(gen1);
        store.WriteAtomic(Bytes(72));
        WriteFile(temp.Path, PrimaryName, string.Empty);

        var next = new AtomicFileSaveStore(temp.Path);
        Assert.Equal(SaveReadOutcome.IoFailure, next.ReadPrimary().Outcome);
        Assert.Equal(SaveReadOutcome.Success, next.ReadBackup().Outcome);
        Assert.True(gen1.SequenceEqual(next.ReadBackup().EnvelopeBytes!));
    }

    // ------------------------------------------------------------------
    // Access failures are diagnosed, never misclassified as "no save found".
    // ------------------------------------------------------------------

    [Fact]
    public void ReadPath_NamesADirectory_ReportsIoFailureInsteadOfThrowing()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, PrimaryName));
        Directory.CreateDirectory(Path.Combine(temp.Path, BackupName));

        var store = new AtomicFileSaveStore(temp.Path);

        var primary = store.ReadPrimary();
        Assert.Equal(SaveReadOutcome.IoFailure, primary.Outcome);
        Assert.False(string.IsNullOrEmpty(primary.Detail));

        var backup = store.ReadBackup();
        Assert.Equal(SaveReadOutcome.IoFailure, backup.Outcome);
    }

    [Fact]
    public void Constructor_MissingDirectory_CreatesIt()
    {
        using var temp = new TempDirectory();
        var nested = Path.Combine(temp.Path, "profiles", "default");
        var store = new AtomicFileSaveStore(nested);
        Assert.Equal(SaveReadOutcome.NotFound, store.ReadPrimary().Outcome);
        Assert.True(Directory.Exists(nested));
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private static void CorruptFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteFile(string directory, string name, string content) =>
        File.WriteAllText(Path.Combine(directory, name), content);

    private static byte[] Bytes(int length) =>
        Enumerable.Range(0, length).Select(i => (byte)((i * 31 + 7) % 256)).ToArray();
}
