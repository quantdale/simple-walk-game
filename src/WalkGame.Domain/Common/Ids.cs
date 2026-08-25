using System;

namespace WalkGame.Domain.Common
{
    /// <summary>
    /// Marker kinds for strongly-typed stable identifiers. Each marker exists so that
    /// <see cref="Id{TKind}"/> instances cannot be mixed between entity types.
    /// </summary>
    public sealed class RegionIdKind { private RegionIdKind() { } }

    public sealed class LandmarkIdKind { private LandmarkIdKind() { } }

    public sealed class ProjectIdKind { private ProjectIdKind() { } }

    public sealed class ProducerIdKind { private ProducerIdKind() { } }

    public sealed class DiscoveryIdKind { private DiscoveryIdKind() { } }

    public sealed class ExpeditionIdKind { private ExpeditionIdKind() { } }

    public sealed class RewardTransactionIdKind { private RewardTransactionIdKind() { } }

    /// <summary>
    /// Immutable, ordinal-compared string identifier. Save data stores these values,
    /// never object references or array indices.
    /// </summary>
    public readonly struct Id<TKind> : IEquatable<Id<TKind>>, IComparable<Id<TKind>>
        where TKind : class
    {
        public string Value { get; }

        public Id(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID value must be a non-empty string.", nameof(value));
            Value = value;
        }

        public static Id<TKind> FromGuid(Guid guid) => new Id<TKind>(guid.ToString("N"));

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(Id<TKind> other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is Id<TKind> other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public int CompareTo(Id<TKind> other) => string.CompareOrdinal(Value ?? string.Empty, other.Value ?? string.Empty);

        public static bool operator ==(Id<TKind> left, Id<TKind> right) => left.Equals(right);

        public static bool operator !=(Id<TKind> left, Id<TKind> right) => !left.Equals(right);
    }
}
