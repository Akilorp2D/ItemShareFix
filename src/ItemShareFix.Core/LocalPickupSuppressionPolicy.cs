namespace ItemShareFix.Core
{
    /// <summary>
    /// Pure policy for suppressing a retained shared pickup only from the
    /// perspective of a local collector. This policy never authorizes authoritative object,
    /// collider, network or provider-state mutation.
    /// </summary>
    public static class LocalPickupSuppressionPolicy
    {
        public static bool ShouldSuppressProcessVisual(
            bool featureEnabled,
            bool upstreamHideCollectedEnabled,
            int localParticipantCount,
            int collectedLocalParticipantCount)
            => featureEnabled
               && upstreamHideCollectedEnabled
               && localParticipantCount > 0
               && collectedLocalParticipantCount == localParticipantCount;

        public static bool ShouldSuppressInteractor(
            bool featureEnabled,
            bool upstreamHideCollectedEnabled,
            bool shareablePickup,
            bool interactorIsLocal,
            bool interactorHasCollected)
            => featureEnabled
               && upstreamHideCollectedEnabled
               && shareablePickup
               && interactorIsLocal
               && interactorHasCollected;

        public static bool AllowsAuthoritativePickupDestroy => false;
        public static bool AllowsSharedRootDisable => false;
        public static bool AllowsSharedColliderDisable => false;
    }
}
