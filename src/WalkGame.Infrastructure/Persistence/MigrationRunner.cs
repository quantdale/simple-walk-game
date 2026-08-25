using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace WalkGame.Infrastructure.Persistence
{
    /// <summary>
    /// One sequential, deterministic migration step between payload schema versions.
    /// Migrations must be pure functions over the payload node and must not depend on
    /// scenes, platform services or randomness.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        string MigrationId { get; }

        /// <summary>Transforms a v{FromVersion} payload into a v{ToVersion} payload. Throws to signal failure.</summary>
        JsonNode Migrate(JsonNode payload);
    }

    /// <summary>
    /// Sequential migration pipeline. The chain is validated up front; each migration runs
    /// on a clone so a failure preserves the original recoverable data.
    /// </summary>
    public sealed class MigrationRunner
    {
        private readonly List<ISaveMigration> _chain;

        public MigrationRunner(IEnumerable<ISaveMigration> migrations)
        {
            _chain = new List<ISaveMigration>(migrations ?? throw new ArgumentNullException(nameof(migrations)));
            _chain.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));

            for (int i = 1; i < _chain.Count; i++)
            {
                if (_chain[i - 1].ToVersion != _chain[i].FromVersion)
                    throw new InvalidOperationException(
                        $"Migration chain is not contiguous: '{_chain[i - 1].MigrationId}' targets v{_chain[i - 1].ToVersion} " +
                        $"but '{_chain[i].MigrationId}' starts at v{_chain[i].FromVersion}.");
                if (_chain[i - 1].FromVersion == _chain[i - 1].ToVersion)
                    throw new InvalidOperationException($"Migration '{_chain[i - 1].MigrationId}' must change the version.");
            }
        }

        /// <summary>Returns true when no migration is needed (payload already current).</summary>
        public bool TryMigrate(int fromVersion, int toVersion, ref JsonNode payload, out IReadOnlyList<string> appliedIds, out string error)
        {
            appliedIds = Array.Empty<string>();
            error = string.Empty;

            if (fromVersion == toVersion)
                return true;

            if (fromVersion > toVersion)
            {
                error = $"Save schema v{fromVersion} is newer than this build supports (v{toVersion}).";
                return false;
            }

            int current = fromVersion;
            var applied = new List<string>();
            while (current < toVersion)
            {
                ISaveMigration? next = null;
                foreach (var migration in _chain)
                {
                    if (migration.FromVersion == current)
                    {
                        next = migration;
                        break;
                    }
                }

                if (next == null)
                {
                    error = $"No registered migration from schema v{current} toward v{toVersion}.";
                    return false;
                }

                try
                {
                    var working = payload.DeepClone();
                    payload = next.Migrate(working);
                }
                catch (Exception ex)
                {
                    error = $"Migration '{next.MigrationId}' failed: {ex.Message}";
                    return false;
                }

                applied.Add(next.MigrationId);
                current = next.ToVersion;
            }

            appliedIds = applied;
            return true;
        }
    }
}
