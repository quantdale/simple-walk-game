using System;
using System.IO;
using System.Text.Json;
using WalkGame.Application.Ux;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// File-backed implementation of the local UX-preferences port (D-042).
    ///
    /// Proportionate to the low value of the data: a single atomic file with NO backup
    /// generations (unlike the canonical save). Worst case on damage is that preferences
    /// reset to documented defaults — never a gameplay loss, because this store is
    /// physically separate from the canonical save envelope.
    ///
    /// Load policy:
    ///   absent file            → NotFound  (defaults)
    ///   malformed JSON         → Malformed (defaults)
    ///   schemaVersion &gt; 1     → FutureVersion (defaults; payload never interpreted)
    ///   schemaVersion &lt; 1     → Malformed (defaults)
    ///   present v1 fields      → merged over defaults, so hand-trimmed payloads keep
    ///                            explicit default semantics for missing keys.
    ///
    /// Write sequence mirrors the save store's atomic pattern: durable temp write, then
    /// delete+move replace. A crash leaves either the old or the new file intact; leftover
    /// temporaries are ignored by reads and cleaned before the next write.
    /// </summary>
    public sealed class LocalPreferencesStore : IUxPreferencesStore
    {
        private readonly string _path;
        private readonly string _tempPath;

        public LocalPreferencesStore(string directory, string fileName = "ux-preferences.json")
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, fileName);
            _tempPath = Path.Combine(directory, fileName + ".tmp");
        }

        public UxPreferencesLoadResult Load()
        {
            byte[] bytes;
            try
            {
                if (!File.Exists(_path))
                    return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.NotFound);

                bytes = File.ReadAllBytes(_path);
            }
            catch (IOException ex)
            {
                return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.IoFailure, detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.IoFailure, detail: ex.Message);
            }

            if (bytes.Length == 0)
                return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.Malformed, detail: "Preferences file is empty.");

            PreferencesDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<PreferencesDto>(bytes, SaveJson.Options);
            }
            catch (JsonException ex)
            {
                return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.Malformed, detail: ex.Message);
            }

            if (dto == null || dto.SchemaVersion != UxPreferencesState.CurrentVersion)
            {
                var outcome = dto != null && dto.SchemaVersion > UxPreferencesState.CurrentVersion
                    ? UxPreferencesLoadOutcome.FutureVersion
                    : UxPreferencesLoadOutcome.Malformed;
                int found = dto?.SchemaVersion ?? -1;
                return new UxPreferencesLoadResult(outcome,
                    detail: "Preferences schema version " + found.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                            " (supported: " + UxPreferencesState.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").");
            }

            var state = UxPreferencesState.CreateDefault();
            dto.ApplyTo(state);
            state.SchemaVersion = UxPreferencesState.CurrentVersion;
            return new UxPreferencesLoadResult(UxPreferencesLoadOutcome.Success, state);
        }

        public void Save(UxPreferencesState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var dto = PreferencesDto.FromState(state);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(dto, SaveJson.Options);

            WriteDurable(_tempPath, bytes);
            ReplaceFile(_tempPath, _path);
        }

        private static void ReplaceFile(string sourcePath, string destinationPath)
        {
            // netstandard2.1 lacks File.Move(overwrite); same contract as the save store.
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(sourcePath, destinationPath);
        }

        private static void WriteDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }

        /// <summary>
        /// Nullable DTO so every persisted key is individually optional and merges over
        /// documented defaults — absent keys mean "default", never CLR zero-values.
        /// Unknown future keys inside a v1 payload are ignored by the serializer.
        /// </summary>
        internal sealed class PreferencesDto
        {
            public int SchemaVersion { get; set; }

            public OnboardingStage? OnboardingStage { get; set; }
            public bool? ReducedMotion { get; set; }
            public bool? HapticsEnabled { get; set; }
            public bool? SoundEnabled { get; set; }
            public bool? NotificationsOptIn { get; set; }
            public bool? NotifyProjectCompletions { get; set; }
            public bool? NotifyExpeditionResults { get; set; }
            public bool? NotifyDiscoveries { get; set; }
            public bool? DailyReminderEnabled { get; set; }
            public int? DailyReminderMinutesOfDay { get; set; }
            public bool? DiagnosticsVisible { get; set; }

            internal static PreferencesDto FromState(UxPreferencesState state) => new PreferencesDto
            {
                SchemaVersion = state.SchemaVersion,
                OnboardingStage = state.OnboardingStage,
                ReducedMotion = state.ReducedMotion,
                HapticsEnabled = state.HapticsEnabled,
                SoundEnabled = state.SoundEnabled,
                NotificationsOptIn = state.NotificationsOptIn,
                NotifyProjectCompletions = state.NotifyProjectCompletions,
                NotifyExpeditionResults = state.NotifyExpeditionResults,
                NotifyDiscoveries = state.NotifyDiscoveries,
                DailyReminderEnabled = state.DailyReminderEnabled,
                DailyReminderMinutesOfDay = state.DailyReminderMinutesOfDay,
                DiagnosticsVisible = state.DiagnosticsVisible,
            };

            internal void ApplyTo(UxPreferencesState state)
            {
                if (OnboardingStage.HasValue) state.OnboardingStage = OnboardingStage.Value;
                if (ReducedMotion.HasValue) state.ReducedMotion = ReducedMotion.Value;
                if (HapticsEnabled.HasValue) state.HapticsEnabled = HapticsEnabled.Value;
                if (SoundEnabled.HasValue) state.SoundEnabled = SoundEnabled.Value;
                if (NotificationsOptIn.HasValue) state.NotificationsOptIn = NotificationsOptIn.Value;
                if (NotifyProjectCompletions.HasValue) state.NotifyProjectCompletions = NotifyProjectCompletions.Value;
                if (NotifyExpeditionResults.HasValue) state.NotifyExpeditionResults = NotifyExpeditionResults.Value;
                if (NotifyDiscoveries.HasValue) state.NotifyDiscoveries = NotifyDiscoveries.Value;
                if (DailyReminderEnabled.HasValue) state.DailyReminderEnabled = DailyReminderEnabled.Value;
                if (DailyReminderMinutesOfDay.HasValue) state.DailyReminderMinutesOfDay = DailyReminderMinutesOfDay.Value;
                if (DiagnosticsVisible.HasValue) state.DiagnosticsVisible = DiagnosticsVisible.Value;
            }
        }
    }
}
