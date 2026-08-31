namespace ItemShareFix.Core
{
    /// <summary>
    /// Client-safe presentation-only participant classification. It intentionally has no server death/out-of-lives
    /// input. Exact Remote Operation wins over ordinary body presence; authoritative server claim classification is
    /// owned separately by ParticipantClassifier.
    /// </summary>
    public static class LocalParticipantPresentationPolicy
    {
        public static ParticipantState Classify(bool masterAvailable, bool exactRemoteOperationSignal, bool bodyPresent)
        {
            if (!masterAvailable) return ParticipantState.Disconnected;
            if (exactRemoteOperationSignal) return ParticipantState.SupportDrone;
            if (bodyPresent) return ParticipantState.Alive;
            return ParticipantState.FullyDead;
        }
    }
}
