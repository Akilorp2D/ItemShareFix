namespace ItemShareFix.Core
{
    public enum ImmediateGiveDecision
    {
        AllowUpstream = 0,
        SuppressHistoricalBarrier = 1,
        SuppressFullyDeadDeferred = 2,
        SuppressExistingDeferredEntitlement = 3,
    }

    public static class ImmediateGivePolicy
    {
        public static ImmediateGiveDecision Decide(
            bool stableIdentityResolved,
            ParticipantState? participantState,
            bool deadPlayerDeferredItemsEnabled,
            bool historicalBarrierForCurrentPickup,
            bool deferredEntitlementReadyForCurrentPickup = false)
        {
            // Unknown stable identity can never justify suppression.
            if (!stableIdentityResolved)
                return ImmediateGiveDecision.AllowUpstream;

            // A historical barrier is stage-local, pickup-exact, and tied to an already-proven
            // stable user. It remains authoritative even while current participant-state probing
            // is transiently frozen.
            if (historicalBarrierForCurrentPickup)
                return ImmediateGiveDecision.SuppressHistoricalBarrier;

            // A payload-backed DEFERRED record means ItemShareFix already became authoritative for
            // this exact pickup/participant by suppressing an earlier upstream grant. Preserve that
            // entitlement through transient probe uncertainty and prevent a duplicate upstream grant.
            if (deferredEntitlementReadyForCurrentPickup)
            {
                if (participantState.HasValue
                    && participantState.Value == ParticipantState.FullyDead
                    && deadPlayerDeferredItemsEnabled)
                    return ImmediateGiveDecision.SuppressFullyDeadDeferred;
                return ImmediateGiveDecision.SuppressExistingDeferredEntitlement;
            }

            // Current-state uncertainty must not be interpreted as FULLY_DEAD, and FULLY_DEAD alone
            // is not sufficient unless a concrete deferred entitlement/payload exists for this pickup.
            return ImmediateGiveDecision.AllowUpstream;
        }

        public static bool ShouldSuppress(
            bool stableIdentityResolved,
            ParticipantState? participantState,
            bool deadPlayerDeferredItemsEnabled,
            bool historicalBarrierForCurrentPickup,
            bool deferredEntitlementReadyForCurrentPickup = false)
            => Decide(
                stableIdentityResolved,
                participantState,
                deadPlayerDeferredItemsEnabled,
                historicalBarrierForCurrentPickup,
                deferredEntitlementReadyForCurrentPickup)
               != ImmediateGiveDecision.AllowUpstream;
    }
}
