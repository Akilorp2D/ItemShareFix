using System.Collections.Generic;
using System.Linq;

namespace ItemShareFix.Core
{
    public static class ProjectionPolicy
    {
        public static bool HideOrdinaryPickup(bool featureEnabled, bool upstreamPreferenceEnabled, IEnumerable<bool> localCollectedStates)
        {
            if (!featureEnabled || !upstreamPreferenceEnabled) return false;
            var states = localCollectedStates.ToArray();
            return states.Length > 0 && states.All(x => x);
        }

        public static bool ShowPersonalMarker(bool featureEnabled, ParticipantState localParticipantState, bool collected, bool historicallyBlocked = false)
        {
            if (!featureEnabled || collected || historicallyBlocked) return false;
            return localParticipantState == ParticipantState.Alive || localParticipantState == ParticipantState.SupportDrone;
        }
    }
}
