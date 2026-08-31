using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public readonly struct LocalCommandPresentationDecision
    {
        public LocalCommandPresentationDecision(bool allLocalStateResolved, bool anyLocalPending, bool suppressWorldPresentation)
        {
            AllLocalStateResolved = allLocalStateResolved;
            AnyLocalPending = anyLocalPending;
            SuppressWorldPresentation = suppressWorldPresentation;
        }

        public bool AllLocalStateResolved { get; }
        public bool AnyLocalPending { get; }
        public bool SuppressWorldPresentation { get; }
    }

    /// <summary>
    /// Process-local presentation policy for one shared Command picker. A single world object can only be hidden when
    /// every local participant on this process has an exact resolved picked=true state. Pending or unresolved state
    /// always fails open so the authoritative network picker remains visibly available.
    /// </summary>
    public static class LocalCommandPresentationPolicy
    {
        public static LocalCommandPresentationDecision Evaluate(IReadOnlyList<bool?> localPickedStates)
        {
            if (localPickedStates == null) throw new ArgumentNullException(nameof(localPickedStates));
            if (localPickedStates.Count == 0)
                return new LocalCommandPresentationDecision(false, false, false);

            var allResolved = true;
            var anyPending = false;
            for (var i = 0; i < localPickedStates.Count; i++)
            {
                var state = localPickedStates[i];
                if (!state.HasValue)
                {
                    allResolved = false;
                    continue;
                }
                if (!state.Value) anyPending = true;
            }

            var suppress = allResolved && !anyPending;
            return new LocalCommandPresentationDecision(allResolved, anyPending, suppress);
        }
    }
}
