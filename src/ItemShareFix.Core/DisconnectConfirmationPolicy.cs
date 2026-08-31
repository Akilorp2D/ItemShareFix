namespace ItemShareFix.Core
{
    public enum NetworkDestroyDisposition
    {
        IgnoreStillAuthoritative = 0,
        HoldForAuthoritativeConfirmation = 1,
    }

    /// <summary>
    /// Separates transient network-object destruction from an authoritative participant disconnect. Only confirmed
    /// absence after the configured grace boundary may cancel pending/deferred entitlement.
    /// </summary>
    public static class DisconnectConfirmationPolicy
    {
        // A generic NetworkBehaviour.OnNetworkDestroy callback is lifecycle evidence only.
        // It is never sufficient on its own to transition participant entitlement state.
        public static NetworkDestroyDisposition EvaluateNetworkDestroy(bool participantResolved, bool authoritativePresence)
            => participantResolved && authoritativePresence
                ? NetworkDestroyDisposition.IgnoreStillAuthoritative
                : NetworkDestroyDisposition.HoldForAuthoritativeConfirmation;

        public static bool ShouldConfirmDisconnect(bool participantAbsentFromAuthoritativeControllerList, bool absenceGraceElapsed)
            => participantAbsentFromAuthoritativeControllerList && absenceGraceElapsed;

        public static bool AllowsImmediateCancelFromNetworkDestroy => false;
        public static bool UsesObjectNamePrefabOrBodyHeuristics => false;
    }
}
