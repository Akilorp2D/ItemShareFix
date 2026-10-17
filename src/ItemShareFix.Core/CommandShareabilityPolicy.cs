using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public enum CommandShareabilityState
    {
        Unresolved,
        AllShareable,
        AllUnshareable,
        Mixed,
    }

    public readonly struct CommandShareabilityDecision
    {
        public CommandShareabilityDecision(CommandShareabilityState state)
        {
            State = state;
        }

        public CommandShareabilityState State { get; }
        public bool MarkerEligible => State == CommandShareabilityState.AllShareable;

        public string DiagnosticToken
        {
            get
            {
                switch (State)
                {
                    case CommandShareabilityState.AllShareable: return "all-shareable";
                    case CommandShareabilityState.AllUnshareable: return "all-unshareable";
                    case CommandShareabilityState.Mixed: return "mixed";
                    default: return "unresolved";
                }
            }
        }

        public string FilterReason
        {
            get
            {
                switch (State)
                {
                    case CommandShareabilityState.AllUnshareable: return "upstream-not-shareable";
                    case CommandShareabilityState.Mixed: return "mixed-shareability";
                    case CommandShareabilityState.Unresolved: return "shareability-unresolved";
                    default: return string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Pure aggregation only. Production obtains each boolean from exact ItemShare 1.7.1 IsShareable(PickupDef);
    /// this type intentionally contains no tier/config rules of its own.
    /// </summary>
    public static class CommandShareabilityPolicy
    {
        // Exact exclusions are removed from the candidate set, not treated as an
        // ordinary upstream-unshareable option that would veto a mixed picker.
        public static CommandShareabilityDecision Evaluate(IReadOnlyList<bool?> optionShareability, IReadOnlyList<bool> excludedOptions)
        {
            if (optionShareability.Count != excludedOptions.Count)
                return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);
            var included = new List<bool?>(optionShareability.Count);
            var excludedAny = false;
            for (var i = 0; i < optionShareability.Count; i++)
            {
                if (excludedOptions[i]) excludedAny = true;
                else included.Add(optionShareability[i]);
            }
            if (included.Count == 0 && excludedAny)
                return new CommandShareabilityDecision(CommandShareabilityState.AllUnshareable);
            return Evaluate(included);
        }

        public static CommandShareabilityDecision Evaluate(IEnumerable<bool?>? optionShareability)
        {
            if (optionShareability == null) return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);

            var sawValue = false;
            var anyShareable = false;
            var anyUnshareable = false;
            foreach (var value in optionShareability)
            {
                sawValue = true;
                if (!value.HasValue) return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);
                if (value.Value) anyShareable = true;
                else anyUnshareable = true;
            }

            if (!sawValue) return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);
            if (anyShareable && anyUnshareable) return new CommandShareabilityDecision(CommandShareabilityState.Mixed);
            if (anyShareable) return new CommandShareabilityDecision(CommandShareabilityState.AllShareable);
            return new CommandShareabilityDecision(CommandShareabilityState.AllUnshareable);
        }
    }
}
