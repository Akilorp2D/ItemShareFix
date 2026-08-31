namespace ItemShareFix.Core
{
    /// <summary>
    /// Policy for wiring exact blocking-modal lifecycle/factory signals into the cached presentation probe.
    /// The immediate path is independent from the rare global UI fallback cadence.
    /// </summary>
    public static class BlockingModalLifecyclePolicy
    {
        public const string PauseScreenControllerTypeName = "RoR2.UI.PauseScreenController";
        public const string SimpleDialogBoxTypeName = "RoR2.UI.SimpleDialogBox";
        public const string PickupPickerPanelTypeName = "RoR2.UI.PickupPickerPanel";

        public static bool IsKnownBlockingModalTypeName(string? typeName)
            => typeName == PauseScreenControllerTypeName
               || typeName == SimpleDialogBoxTypeName
               || typeName == PickupPickerPanelTypeName;

        public static bool ShouldSeedObservedCandidate(bool knownBlockingType, bool sceneObjectValid)
            => knownBlockingType && sceneObjectValid;

        public static bool ShouldAddObservedInstance(bool instanceAlreadyCached)
            => !instanceAlreadyCached;

        public static bool LifecycleSeedRequiresGlobalDiscovery(bool observedCandidateCached)
            => !observedCandidateCached;

        public static bool SuppressionStateChanged(bool previousSuppressed, bool currentSuppressed)
            => previousSuppressed != currentSuppressed;

        public static bool PreserveRareFallbackCadence(float fallbackSeconds)
            => fallbackSeconds >= MarkerFramePipelinePolicy.BlockingModalFallbackDiscoverySeconds;
    }
}
