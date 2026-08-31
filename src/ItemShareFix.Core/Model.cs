using System;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Authoritative participation state used by sharing policy. SupportDrone remains active; FullyDead may defer
    /// entitlement; Disconnected is distinct from death and is reached only after authoritative absence is confirmed.
    /// </summary>
    public enum ParticipantState
    {
        Alive = 0,
        SupportDrone = 1,
        FullyDead = 2,
        Disconnected = 3,
    }

    /// <summary>
    /// Lifecycle of one pickup entitlement for one participant generation. Terminal states prevent duplicate grants,
    /// while Deferred preserves entitlement across a fully-dead interval until a safe restored-player point.
    /// </summary>
    public enum ClaimState
    {
        Pending = 0,
        Collected = 1,
        Deferred = 2,
        GrantedDeferred = 3,
        CancelledDisconnected = 4,
    }

    public enum HistoricalClaimState
    {
        Collected = 0,
        CancelledDisconnected = 1,
        GrantedDeferred = 2,
    }

    /// <summary>Stable identity that survives body/master replacement and is used for historical entitlement barriers.</summary>
    public readonly struct StableUserKey : IEquatable<StableUserKey>
    {
        public StableUserKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Stable user identity is required.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(StableUserKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StableUserKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    /// <summary>Pairs stable user identity with a bounded participation generation so reconnects cannot revive terminal claims.</summary>
    public readonly struct ParticipantKey : IEquatable<ParticipantKey>
    {
        public ParticipantKey(StableUserKey stableUser, string generation)
        {
            if (string.IsNullOrWhiteSpace(generation)) throw new ArgumentException("Participation generation is required.", nameof(generation));
            StableUser = stableUser;
            Generation = generation;
        }

        public StableUserKey StableUser { get; }
        public string Generation { get; }
        public string Value => StableUser.Value + "|generation=" + Generation;
        public bool Equals(ParticipantKey other) => StableUser.Equals(other.StableUser) && string.Equals(Generation, other.Generation, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ParticipantKey other && Equals(other);
        public override int GetHashCode() => unchecked((StableUser.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(Generation));
        public override string ToString() => Value;
    }

    public readonly struct SharedPickupKey : IEquatable<SharedPickupKey>
    {
        public SharedPickupKey(int value)
        {
            if (value == 0) throw new ArgumentOutOfRangeException(nameof(value), "Unity instance id 0 is not a valid pickup identity.");
            Value = value;
        }

        public int Value { get; }
        public bool Equals(SharedPickupKey other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is SharedPickupKey other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public readonly struct ClaimKey : IEquatable<ClaimKey>
    {
        public ClaimKey(SharedPickupKey pickup, ParticipantKey participant)
        {
            Pickup = pickup;
            Participant = participant;
        }

        public SharedPickupKey Pickup { get; }
        public ParticipantKey Participant { get; }
        public bool Equals(ClaimKey other) => Pickup.Equals(other.Pickup) && Participant.Equals(other.Participant);
        public override bool Equals(object? obj) => obj is ClaimKey other && Equals(other);
        public override int GetHashCode() => unchecked((Pickup.GetHashCode() * 397) ^ Participant.GetHashCode());
        public override string ToString() => Pickup + "/" + Participant;
    }

    public readonly struct HistoricalClaimKey : IEquatable<HistoricalClaimKey>
    {
        public HistoricalClaimKey(SharedPickupKey pickup, StableUserKey stableUser)
        {
            Pickup = pickup;
            StableUser = stableUser;
        }

        public SharedPickupKey Pickup { get; }
        public StableUserKey StableUser { get; }
        public bool Equals(HistoricalClaimKey other) => Pickup.Equals(other.Pickup) && StableUser.Equals(other.StableUser);
        public override bool Equals(object? obj) => obj is HistoricalClaimKey other && Equals(other);
        public override int GetHashCode() => unchecked((Pickup.GetHashCode() * 397) ^ StableUser.GetHashCode());
        public override string ToString() => Pickup + "/stable=" + StableUser;
    }

    public sealed class ClaimRecord
    {
        internal ClaimRecord(ClaimKey key, ClaimState state, int createdStage)
        {
            Key = key;
            State = state;
            CreatedStage = createdStage;
        }

        public ClaimKey Key { get; internal set; }
        public ClaimState State { get; internal set; }
        public int CreatedStage { get; }
        public int? TerminalStage { get; internal set; }
    }

    public sealed class HistoricalClaimRecord
    {
        internal HistoricalClaimRecord(HistoricalClaimKey key, HistoricalClaimState state, int stage)
        {
            Key = key;
            State = state;
            Stage = stage;
        }

        public HistoricalClaimKey Key { get; internal set; }
        public HistoricalClaimState State { get; internal set; }
        public int Stage { get; internal set; }
    }
}
