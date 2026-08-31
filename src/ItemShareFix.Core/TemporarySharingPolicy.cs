namespace ItemShareFix.Core
{
    /// <summary>
    /// Temporary-sharing authority boundary. OFF uses vanilla only before upstream ItemShare has
    /// established state for the same interaction; existing ItemShare state remains authoritative.
    /// </summary>
    public static class TemporarySharingPolicy
    {
        public static bool ShouldUseVanillaBypass(
            bool isTemporary,
            bool shareTemporaryItems,
            bool upstreamInteractionAlreadyOwned)
            => isTemporary && !shareTemporaryItems && !upstreamInteractionAlreadyOwned;
    }
}
