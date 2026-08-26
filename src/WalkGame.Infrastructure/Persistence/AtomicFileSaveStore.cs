using System;
using System.IO;
using WalkGame.Application.Persistence;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// Durable file store with atomic commit and one-generation backup.
    ///
    /// Write sequence (WriteAtomic):
    ///   1. envelope bytes → slot.tmp   (durable flush)
    ///   2. existing primary → backup.tmp → backup   (previous good copy retained)
    ///   3. slot.tmp → primary          (atomic replace)
    ///
    /// Recovery sequence (WriteAtomicPreservingBackup), used after boot fell back to the
    /// backup because the primary failed decode/validation:
    ///   1. envelope bytes → slot.tmp   (durable flush)
    ///   2. slot.tmp → primary          (atomic replace; backup untouched)
    ///
    /// A crash at any point leaves either the old primary or the new primary intact, and
    /// the most recent healthy generation recoverable from backup. A known-bad primary is
    /// never promoted into the backup slot, so recovering can never trade the last valid
    /// generation for garbage.
    /// </summary>
    public sealed class AtomicFileSaveStore : ISaveStore
    {
        private readonly string _primaryPath;
        private readonly string _backupPath;
        private readonly string _tempPath;
        private readonly string _backupTempPath;

        public AtomicFileSaveStore(string directory, string slotName = "save")
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(slotName))
                throw new ArgumentException("Slot name is required.", nameof(slotName));

            Directory.CreateDirectory(directory);
            _primaryPath = Path.Combine(directory, slotName + ".json");
            _backupPath = Path.Combine(directory, slotName + ".backup.json");
            _tempPath = Path.Combine(directory, slotName + ".tmp");
            _backupTempPath = Path.Combine(directory, slotName + ".backup.tmp");

            CleanupStaleTemporaries();
        }

        public SaveReadResult ReadPrimary() => ReadFile(_primaryPath);

        public SaveReadResult ReadBackup() => ReadFile(_backupPath);

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (envelopeBytes == null) throw new ArgumentNullException(nameof(envelopeBytes));

            WriteDurable(_tempPath, envelopeBytes);

            if (File.Exists(_primaryPath))
            {
                CopyToDurableFile(_primaryPath, _backupTempPath);
                ReplaceFile(_backupTempPath, _backupPath);
            }

            ReplaceFile(_tempPath, _primaryPath);
        }

        /// <inheritdoc />
        public void WriteAtomicPreservingBackup(byte[] envelopeBytes)
        {
            if (envelopeBytes == null) throw new ArgumentNullException(nameof(envelopeBytes));

            WriteDurable(_tempPath, envelopeBytes);
            ReplaceFile(_tempPath, _primaryPath);
        }

        /// <summary>
        /// netstandard2.1 lacks File.Move(overwrite). Delete+Move keeps the safety contract:
        /// a crash between the two steps leaves the previous generation in backup.
        /// </summary>
        private static void ReplaceFile(string sourcePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>
        /// Streams the previous primary generation into the backup slot without holding
        /// the whole save in memory. The copy becomes durable before it is promoted,
        /// exactly like <see cref="WriteDurable"/> did for the buffered variant.
        /// </summary>
        private static void CopyToDurableFile(string sourcePath, string destinationPath)
        {
            using (var source = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096))
            {
                source.CopyTo(destination, bufferSize: 16384);
                destination.Flush(flushToDisk: true);
            }
        }

        private static SaveReadResult ReadFile(string path)
        {
            if (!File.Exists(path))
            {
                // File.Exists also returns false when the file's metadata cannot be
                // accessed at all (for example permission-denied). Probe once so an
                // intact-but-inaccessible save is diagnosed as an I/O failure instead of
                // being misreported as absent — "no save found" must mean the save is
                // really not there, never "we could not look".
                var accessFailure = ClassifyInaccessible(path);
                if (accessFailure != null)
                    return accessFailure;

                return SaveReadResult.Fail(SaveReadOutcome.NotFound, $"No file at '{path}'.");
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    return SaveReadResult.Fail(SaveReadOutcome.IoFailure, $"File '{path}' is empty.");
                return SaveReadResult.Ok(bytes);
            }
            catch (IOException ex)
            {
                return SaveReadResult.Fail(SaveReadOutcome.IoFailure, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return SaveReadResult.Fail(
                    SaveReadOutcome.IoFailure,
                    $"Access denied reading '{path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Returns an IoFailure result when the path exists but cannot be opened for
        /// reading (access denied, or the path names a directory). Returns null when the
        /// probe confirms the file is genuinely absent/unreadable-as-missing.
        /// </summary>
        private static SaveReadResult? ClassifyInaccessible(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                return SaveReadResult.Fail(
                    SaveReadOutcome.IoFailure,
                    $"Access denied reading '{path}': {ex.Message}");
            }
            catch (IOException ex)
            {
                return SaveReadResult.Fail(SaveReadOutcome.IoFailure, ex.Message);
            }

            // The probe succeeded — the file appeared between the existence check and the
            // probe. Report the read outcome honestly rather than NotFound.
            return SaveReadResult.Fail(SaveReadOutcome.IoFailure,
                $"File '{path}' appeared while being checked; retry the read.");
        }

        /// <summary>
        /// Removes leftover temporary files from a previously crashed session. Temp files
        /// are never canonical data (reads ignore them and the next write recreates them),
        /// so deleting them at construction cannot lose progress.
        /// </summary>
        private void CleanupStaleTemporaries()
        {
            TryDeleteQuietly(_tempPath);
            TryDeleteQuietly(_backupTempPath);
        }

        private static void TryDeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort only; the write path tolerates leftover temporaries.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Durable write: Flush(flushToDisk:true) issues FlushFileBuffers, which is the
        /// actual durability barrier the atomic-commit sequence relies on — data reaches
        /// media before any rename step runs. FILE_FLAG_WRITE_THROUGH was deliberately
        /// not requested: with a single buffered write followed by an explicit flush to
        /// disk it adds no durability and measurably slows every commit on Windows.
        /// </summary>
        private static void WriteDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }
    }
}
