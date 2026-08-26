using System;
using System.IO;
using System.Text;
using WalkGame.Application.Ux;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Tests.TestSupport;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-042 store contract: atomic writes, documented degradation for malformed/future
/// payloads, absent-keys-mean-default merge, restart durability, byte-stable rewrites.
/// </summary>
public sealed class LocalPreferencesStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private LocalPreferencesStore NewStore() => new LocalPreferencesStore(_temp.Path);

    [Fact]
    public void Load_WithoutAnyFile_ReturnsNotFound()
    {
        var result = NewStore().Load();

        Assert.Equal(UxPreferencesLoadOutcome.NotFound, result.Outcome);
        Assert.Null(result.State);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var store = NewStore();
        var state = UxPreferencesState.CreateDefault();
        state.OnboardingStage = OnboardingStage.Simulation;
        state.ReducedMotion = true;
        state.HapticsEnabled = false;
        state.SoundEnabled = false;
        state.NotificationsOptIn = true;
        state.NotifyProjectCompletions = false;
        state.NotifyExpeditionResults = false;
        state.NotifyDiscoveries = false;
        state.DailyReminderEnabled = true;
        state.DailyReminderMinutesOfDay = 1234;
        state.DiagnosticsVisible = true;

        store.Save(state);
        var result = store.Load();

        Assert.Equal(UxPreferencesLoadOutcome.Success, result.Outcome);
        var loaded = result.State!;
        Assert.Equal(OnboardingStage.Simulation, loaded.OnboardingStage);
        Assert.True(loaded.ReducedMotion);
        Assert.False(loaded.HapticsEnabled);
        Assert.False(loaded.SoundEnabled);
        Assert.True(loaded.NotificationsOptIn);
        Assert.False(loaded.NotifyProjectCompletions);
        Assert.False(loaded.NotifyExpeditionResults);
        Assert.False(loaded.NotifyDiscoveries);
        Assert.True(loaded.DailyReminderEnabled);
        Assert.Equal(1234, loaded.DailyReminderMinutesOfDay);
        Assert.True(loaded.DiagnosticsVisible);
        Assert.Equal(UxPreferencesState.CurrentVersion, loaded.SchemaVersion);
    }

    [Fact]
    public void Load_MalformedPayload_ReturnsMalformed()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), "{ this is not json");

        var result = NewStore().Load();

        Assert.Equal(UxPreferencesLoadOutcome.Malformed, result.Outcome);
        Assert.Null(result.State);
    }

    [Fact]
    public void Load_EmptyPayload_ReturnsMalformed()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), string.Empty);

        var result = NewStore().Load();

        Assert.Equal(UxPreferencesLoadOutcome.Malformed, result.Outcome);
    }

    [Fact]
    public void Load_FutureSchemaVersion_NeverInterpretsPayload_ReturnsFutureVersion()
    {
        File.WriteAllText(
            Path.Combine(_temp.Path, "ux-preferences.json"),
            "{\"schemaVersion\":99,\"reducedMotion\":true,\"hapticsEnabled\":false}");

        var result = NewStore().Load();

        Assert.Equal(UxPreferencesLoadOutcome.FutureVersion, result.Outcome);
        // The unknown payload is never interpreted: no state object is produced.
        // Defaults are applied by the session layer (covered there).
        Assert.Null(result.State);
    }

    [Fact]
    public void Load_ZeroOrNegativeSchemaVersion_IsMalformed()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), "{\"schemaVersion\":0}");

        Assert.Equal(UxPreferencesLoadOutcome.Malformed, NewStore().Load().Outcome);

        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), "{\"schemaVersion\":-3}");

        Assert.Equal(UxPreferencesLoadOutcome.Malformed, NewStore().Load().Outcome);
    }

    [Fact]
    public void Load_V1PayloadMissingKeys_MergesOverDocumentedDefaults()
    {
        // Only two keys present: everything else must keep default semantics,
        // including the true-valued defaults (haptics/sound/category toggles).
        File.WriteAllText(
            Path.Combine(_temp.Path, "ux-preferences.json"),
            "{\"schemaVersion\":1,\"reducedMotion\":true}");

        var result = NewStore().Load();

        Assert.Equal(UxPreferencesLoadOutcome.Success, result.Outcome);
        var state = result.State!;
        Assert.True(state.ReducedMotion);
        Assert.True(state.HapticsEnabled);
        Assert.True(state.SoundEnabled);
        Assert.False(state.NotificationsOptIn);
        Assert.True(state.NotifyProjectCompletions);
        Assert.Equal(UxPreferencesState.DefaultReminderMinutesOfDay, state.DailyReminderMinutesOfDay);
        Assert.Equal(OnboardingStage.NotStarted, state.OnboardingStage);
    }

    [Fact]
    public void Save_IdenticalStateTwice_IsByteStable()
    {
        var store = NewStore();
        var state = UxPreferencesState.CreateDefault();
        state.OnboardingStage = OnboardingStage.WorldBaseline;
        state.DailyReminderMinutesOfDay = 600;

        store.Save(state.Clone());
        var first = File.ReadAllBytes(Path.Combine(_temp.Path, "ux-preferences.json"));
        store.Save(state.Clone());
        var second = File.ReadAllBytes(Path.Combine(_temp.Path, "ux-preferences.json"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Restart_NewStoreInstance_PreservesUserChoices()
    {
        var first = NewStore();
        var state = UxPreferencesState.CreateDefault();
        state.ReducedMotion = true;
        state.NotificationsOptIn = true;
        state.OnboardingStage = OnboardingStage.FirstProject;
        first.Save(state);

        var second = NewStore();
        var loaded = second.Load();

        Assert.Equal(UxPreferencesLoadOutcome.Success, loaded.Outcome);
        Assert.True(loaded.State!.ReducedMotion);
        Assert.True(loaded.State.NotificationsOptIn);
        Assert.Equal(OnboardingStage.FirstProject, loaded.State.OnboardingStage);
    }

    [Fact]
    public void Load_LeftoverTemporaryFileFromInterruptedWrite_IsIgnored()
    {
        // A crash mid-write leaves only the .tmp; reads must never consume it.
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json.tmp"), "{\"schemaVersion\":1}");
        var store = NewStore();
        store.Save(UxPreferencesState.CreateDefault());

        Assert.Equal(UxPreferencesLoadOutcome.Success, store.Load().Outcome);
        Assert.True(File.Exists(Path.Combine(_temp.Path, "ux-preferences.json")));
    }

    [Fact]
    public void Payload_DoesNotContainCanonicalGameStateFields()
    {
        var store = NewStore();
        var state = UxPreferencesState.CreateDefault();
        state.ReducedMotion = true;
        store.Save(state);

        var text = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_temp.Path, "ux-preferences.json")));

        // The preferences record must never become a shadow copy of canonical state.
        Assert.DoesNotContain("vitality", text.ToLowerInvariant());
        Assert.DoesNotContain("ledger", text.ToLowerInvariant());
        Assert.DoesNotContain("projects", text.ToLowerInvariant());
        Assert.DoesNotContain("queue", text.ToLowerInvariant());
    }
}
