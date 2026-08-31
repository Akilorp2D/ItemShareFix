namespace ItemShareFix.Core
{
    /// <summary>
    /// Pure decision policy for the ItemShare-private IsDown bridge. It intentionally has no
    /// distribution-scope input: ItemShare calls IsDown both from ordinary distribution and
    /// from Collectors/Command paths outside ItemShareFix BeginDistribution.
    /// </summary>
    public static class ItemShareActiveGatePolicy
    {
        public static bool ShouldCorrectIsDown(
            bool upstreamOriginalIsDown,
            bool authoritativeControllerPresent,
            bool exactGenerationProven,
            ParticipantState participantState)
            => upstreamOriginalIsDown
               && authoritativeControllerPresent
               && exactGenerationProven
               && participantState == ParticipantState.SupportDrone;

        public static bool CorrectIsDown(
            bool upstreamOriginalIsDown,
            bool authoritativeControllerPresent,
            bool exactGenerationProven,
            ParticipantState participantState)
            => ShouldCorrectIsDown(
                upstreamOriginalIsDown,
                authoritativeControllerPresent,
                exactGenerationProven,
                participantState)
                ? false
                : upstreamOriginalIsDown;

        public static bool CountsAsOutstandingCollector(bool authoritativeControllerPresent, ParticipantState participantState)
            => authoritativeControllerPresent
               && (participantState == ParticipantState.Alive || participantState == ParticipantState.SupportDrone);

        public static bool IsDeadAutoShareTarget(
            bool upstreamOriginalIsDown,
            bool authoritativeControllerPresent,
            bool exactGenerationProven,
            ParticipantState participantState)
            => CorrectIsDown(
                upstreamOriginalIsDown,
                authoritativeControllerPresent,
                exactGenerationProven,
                participantState);
    }
}
