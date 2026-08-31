using System;

namespace ItemShareFix.Core
{
    public enum GenerationProbeState
    {
        NeverResolved = 0,
        Resolved = 1,
        Frozen = 2,
    }

    public enum UnsupportedProbeDisposition
    {
        NoOwnershipEstablished = 0,
        FreezePreserveExistingState = 1,
    }

    /// <summary>
    /// Tracks whether a single authoritative master generation has ever produced a trustworthy
    /// stable participant identity. A transient probe failure after resolution freezes ItemShareFix
    /// mutation for that generation; it never authorizes destructive entitlement cleanup.
    /// </summary>
    public sealed class GenerationProbeGate
    {
        private ParticipantKey? _provenParticipant;

        public GenerationProbeState State { get; private set; } = GenerationProbeState.NeverResolved;
        public bool HasProvenParticipant => _provenParticipant.HasValue;
        public ParticipantKey ProvenParticipant
            => _provenParticipant ?? throw new InvalidOperationException("No proven participant exists for this generation.");
        public bool CanCreateClaims => State == GenerationProbeState.Resolved;
        public bool CanUseHistoricalBarrier => HasProvenParticipant;

        public bool CanGrantDeferred(ParticipantState exactState)
            => State == GenerationProbeState.Resolved && exactState == ParticipantState.Alive;

        public bool TryResolve(ParticipantKey participant)
        {
            if (_provenParticipant.HasValue && !_provenParticipant.Value.Equals(participant))
                return false;

            _provenParticipant = participant;
            State = GenerationProbeState.Resolved;
            return true;
        }

        public UnsupportedProbeDisposition ObserveUnsupported()
        {
            if (!_provenParticipant.HasValue)
            {
                State = GenerationProbeState.NeverResolved;
                return UnsupportedProbeDisposition.NoOwnershipEstablished;
            }

            State = GenerationProbeState.Frozen;
            return UnsupportedProbeDisposition.FreezePreserveExistingState;
        }
    }
}
