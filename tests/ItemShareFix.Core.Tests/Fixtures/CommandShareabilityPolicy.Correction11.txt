using System.Collections.Generic;
using System.Linq;

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
        public static CommandShareabilityDecision Evaluate(IEnumerable<bool?>? optionShareability)
        {
            if (optionShareability == null) return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);
            var values = optionShareability.ToArray();
            if (values.Length == 0 || values.Any(x => !x.HasValue))
                return new CommandShareabilityDecision(CommandShareabilityState.Unresolved);

            var anyShareable = values.Any(x => x == true);
            var anyUnshareable = values.Any(x => x == false);
            if (anyShareable && anyUnshareable) return new CommandShareabilityDecision(CommandShareabilityState.Mixed);
            if (anyShareable) return new CommandShareabilityDecision(CommandShareabilityState.AllShareable);
            return new CommandShareabilityDecision(CommandShareabilityState.AllUnshareable);
        }
    }
}
