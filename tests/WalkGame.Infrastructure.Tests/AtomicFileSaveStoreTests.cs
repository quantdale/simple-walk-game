using System;
using System.IO;
using System.Linq;
using WalkGame.Application.Persistence;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Tests.TestSupport;
using Xunit;

namespace WalkGame.Infrastructure.Tests;

public sealed class AtomicFileSaveStoreTests
{
    [Fact]
    public void WriteAtomic_ThenReadPrimary_ReturnsExactBytes()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        var bytes = SampleBytes(64);
        store.WriteAtomic(bytes);

        var read = store.ReadPrimary();
        Assert.Equal(SaveReadOutcome.Success, read.Outcome);
        Assert.NotNull(read.EnvelopeBytes);
        Assert.True(bytes.SequenceEqual(read.EnvelopeBytes!));
    }

    [Fact]
    public void SecondWrite_RotatesPreviousPrimaryIntoBackup()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        var first = SampleBytes(32);
        var second = SampleBytes(48);
        store.WriteAtomic(first);
        store.WriteAtomic(second);

        var backup = store.ReadBackup();
        Assert.Equal(SaveReadOutcome.Success, backup.Outcome);
        Assert.True(first.SequenceEqual(backup.EnvelopeBytes!));

        var primary = store.ReadPrimary();
        Assert.True(second.SequenceEqual(primary.EnvelopeBytes!));
    }

    [Fact]
    public void MissingFiles_ReadsReportNotFound()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        Assert.Equal(SaveReadOutcome.NotFound, store.ReadPrimary().Outcome);
        Assert.Equal(SaveReadOutcome.NotFound, store.ReadBackup().Outcome);
    }

    [Fact]
    public void EmptyFiles_AreReportedAsIoFailure()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        File.WriteAllText(temp.FilePath("save.json"), string.Empty);
        File.WriteAllText(temp.FilePath("save.backup.json"), string.Empty);

        Assert.Equal(SaveReadOutcome.IoFailure, store.ReadPrimary().Outcome);
        Assert.Equal(SaveReadOutcome.IoFailure, store.ReadBackup().Outcome);
    }

    [Fact]
    public void StaleTempFile_IsIgnoredByReads_AndConsumedByNextWrite()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        File.WriteAllText(temp.FilePath("save.tmp"), "stale-crash-leftover");
        Assert.Equal(SaveReadOutcome.NotFound, store.ReadPrimary().Outcome);

        var bytes = SampleBytes(24);
        store.WriteAtomic(bytes);

        var read = store.ReadPrimary();
        Assert.Equal(SaveReadOutcome.Success, read.Outcome);
        Assert.True(bytes.SequenceEqual(read.EnvelopeBytes!));
        Assert.False(File.Exists(temp.FilePath("save.tmp")));
    }

    [Fact]
    public void CompletedWrites_NeverAccumulateTemporaryFiles()
    {
        using var temp = new TempDirectory();
        var store = new AtomicFileSaveStore(temp.Path);

        store.WriteAtomic(SampleBytes(16));
        store.WriteAtomic(SampleBytes(20));

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    private static byte[] SampleBytes(int length) =>
        Enumerable.Range(0, length).Select(i => (byte)((i * 37 + 11) % 256)).ToArray();
}
