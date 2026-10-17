using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WalkGame.Application.Presentation;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// D-175 (KI-027 c): file-backed presentation memory, in its own small versioned file next to the
    /// save, physically separate from the canonical save envelope (same pattern as the D-042 UX
    /// preferences file). One atomic file, no backup generations: the worst case on damage is that the
    /// world treats the next open as a baseline and replays nothing, never a gameplay loss.
    ///
    /// Load policy: absent → NotFound; empty/malformed JSON, schemaVersion &lt; 1 or a bad timestamp →
    /// Malformed; schemaVersion &gt; 2 → FutureVersion (v1 and v2 load; D-180) (payload never interpreted); line ids are
    /// normalized (unique, bounded). Write: durable temp file, then an atomic File.Replace (D-179; Move for the first write); leftover
    /// temporaries are ignored by reads and replaced by the next write.
    /// </summary>
    public sealed class LocalPresentationMemoryStore : IPresentationMemoryStore, WalkGame.Application.Persistence.ILocalDataEraser
    {
        public const string DefaultFileName = "presentation-memory.json";
        /// <summary>Files larger than this are not read (Region 1 memory is a few KB).</summary>
        public const int MaxFileBytes = 64 * 1024;

        private readonly string _path;
        private readonly string _tempPath;

        public LocalPresentationMemoryStore(string directory, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, fileName);
            _tempPath = Path.Combine(directory, fileName + ".tmp");
        }

        /// <summary>Deletes the file and its temporary ("Delete all local data").</summary>
        public void EraseAll()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            if (File.Exists(_path)) File.Delete(_path);
        }

        public PresentationMemoryLoadResult Load()
        {
            byte[] bytes;
            try
            {
                if (!File.Exists(_path)) return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.NotFound);
                if (new FileInfo(_path).Length > MaxFileBytes)
                    return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Malformed, detail: "Presentation memory file is too large.");
                bytes = File.ReadAllBytes(_path);
            }
            catch (IOException ex) { return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.IoFailure, detail: ex.GetType().Name); }
            catch (UnauthorizedAccessException ex) { return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.IoFailure, detail: ex.GetType().Name); }

            if (bytes.Length == 0) return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Malformed, detail: "Presentation memory file is empty.");

            MemoryDto? dto;
            try { dto = JsonSerializer.Deserialize<MemoryDto>(bytes, SaveJson.Options); }
            catch (JsonException) { return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Malformed, detail: "Presentation memory is not valid JSON."); }
            catch (NotSupportedException) { return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Malformed, detail: "Presentation memory has an unsupported shape."); }

            if (dto == null || dto.SchemaVersion < 1)
                return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Malformed, detail: "Presentation memory has no valid schema version.");
            if (dto.SchemaVersion > PresentationMemoryState.CurrentVersion)
                return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.FutureVersion, detail: "Presentation memory was written by a newer version.");

            return new PresentationMemoryLoadResult(PresentationMemoryLoadOutcome.Success, new PresentationMemoryState
            {
                SchemaVersion = PresentationMemoryState.CurrentVersion,
                GameCreatedAtUtc = dto.GameCreatedAtUtc,
                ChangesShownThroughUtc = dto.ChangesShownThroughUtc,
                SeenLineIds = PresentationMemoryState.NormalizeLineIds(dto.SeenLineIds),
                // D-180: a v1 file has no such field; null means "derive from the timestamp mark".
                ShownCompletionIds = dto.SchemaVersion >= 2 ? PresentationMemoryState.NormalizeCompletionIds(dto.ShownCompletionIds) : null,
            });
        }

        public void Save(PresentationMemoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var dto = new MemoryDto
            {
                SchemaVersion = PresentationMemoryState.CurrentVersion,
                GameCreatedAtUtc = state.GameCreatedAtUtc,
                ChangesShownThroughUtc = state.ChangesShownThroughUtc,
                SeenLineIds = PresentationMemoryState.NormalizeLineIds(state.SeenLineIds),
                ShownCompletionIds = PresentationMemoryState.NormalizeCompletionIds(state.ShownCompletionIds),
            };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(dto, SaveJson.Options);
            using (var stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
            // D-179: one atomic rename over the existing file (no moment without a committed file);
            // the very first write has nothing to replace.
            if (File.Exists(_path)) File.Replace(_tempPath, _path, destinationBackupFileName: null);
            else File.Move(_tempPath, _path);
        }

        internal sealed class MemoryDto
        {
            public int SchemaVersion { get; set; }
            public DateTimeOffset? GameCreatedAtUtc { get; set; }
            public DateTimeOffset? ChangesShownThroughUtc { get; set; }
            public List<string>? SeenLineIds { get; set; }
            /// <summary>v2 (D-180). Absent in v1 files.</summary>
            public List<string>? ShownCompletionIds { get; set; }
        }
    }
}
