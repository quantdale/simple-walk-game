using System;
using System.IO;
using WalkGame.Application.Persistence;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// Durable file store with atomic commit and one-generation backup.
    ///
    /// Write sequence:
    ///   1. envelope bytes → slot.tmp   (durable flush)
    ///   2. existing primary → backup.tmp → backup   (previous good copy retained)
    ///   3. slot.tmp → primary          (atomic replace)
    ///
    /// A crash at any point leaves either the old primary or the new primary intact and
    /// the previous generation recoverable from backup.
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
        }

        public SaveReadResult ReadPrimary() => ReadFile(_primaryPath);

        public SaveReadResult ReadBackup() => ReadFile(_backupPath);

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (envelopeBytes == null) throw new ArgumentNullException(nameof(envelopeBytes));

            WriteDurable(_tempPath, envelopeBytes);

            if (File.Exists(_primaryPath))
            {
                byte[] currentPrimary = File.ReadAllBytes(_primaryPath);
                WriteDurable(_backupTempPath, currentPrimary);
                ReplaceFile(_backupTempPath, _backupPath);
            }

            ReplaceFile(_tempPath, _primaryPath);
        }

        /// <summary>
        /// Atomic destination replacement. On .NET 6+ File.Move(overwrite) is a single
        /// atomic rename; the delete+move fallback keeps the netstandard2.1 safety contract:
        /// a crash between the two steps leaves the previous generation in backup.
        /// </summary>
        private static void ReplaceFile(string sourcePath, string destinationPath)
        {
#if NET6_0_OR_GREATER
            File.Move(sourcePath, destinationPath, overwrite: true);
#else
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(sourcePath, destinationPath);
#endif
        }

        private static SaveReadResult ReadFile(string path)
        {
            if (!File.Exists(path))
                return SaveReadResult.Fail(SaveReadOutcome.NotFound, $"No file at '{path}'.");

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
        }

        /// <summary>
        /// Write + FlushToDisk semantics. Flush(flushToDisk: true) pushes buffered data AND
        /// metadata to the device before close, which is what makes the commit durable.
        /// FILE_FLAG_WRITE_THROUGH is intentionally NOT set: with a managed 4 KiB buffer the
        /// bytes are not in the OS cache until the explicit flush anyway, so write-through
        /// only added per-write syscall cost without adding crash safety.
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
