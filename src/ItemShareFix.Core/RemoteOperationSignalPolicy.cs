namespace ItemShareFix.Core
{
    /// <summary>
    /// Pure decision policy for the exact per-participant RoR2 Remote Operation signal.
    /// Production may classify SupportDrone only when authoritative-master ownership,
    /// exact runtime shape, invocation success, and the exact boolean signal all agree.
    /// </summary>
    public static class RemoteOperationSignalPolicy
    {
        public static bool ShouldClassifySupportDrone(
            bool authoritativeMasterMatches,
            bool runtimeShapeCompatible,
            bool invocationSucceeded,
            bool exactSignalValue)
            => authoritativeMasterMatches
               && runtimeShapeCompatible
               && invocationSucceeded
               && exactSignalValue;
    }
}
