using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public enum CommandOptionPickupSource
    {
        None,
        NestedPickup,
        DirectCompatibilityFallback,
    }

    public readonly struct CommandOptionPickupDecision<T>
    {
        public CommandOptionPickupDecision(bool hasValue, T value, CommandOptionPickupSource source, bool exactSource, bool disagreement)
        {
            HasValue = hasValue;
            Value = value;
            Source = source;
            ExactSource = exactSource;
            Disagreement = disagreement;
        }

        public bool HasValue { get; }
        public T Value { get; }
        public CommandOptionPickupSource Source { get; }
        public bool ExactSource { get; }
        public bool Disagreement { get; }
    }

    /// <summary>
    /// Chooses the semantic pickup source for a Command option. The nested option.pickup.pickupIndex
    /// path is authoritative because it is the shape consumed by ItemShare's selection path.
    /// A direct option.pickupIndex is compatibility-only and never counts as an exact source.
    /// </summary>
    public static class CommandOptionSourcePolicy
    {
        public const string AuthoritativeSourceToken = "nested-pickup";
        public const string FallbackSourceToken = "direct-fallback";
        public const string UnresolvedSourceToken = "unresolved";

        public static CommandOptionPickupDecision<T> Resolve<T>(
            bool nestedAvailable,
            T nestedValue,
            bool directAvailable,
            T directValue)
        {
            if (nestedAvailable)
            {
                var disagreement = directAvailable && !EqualityComparer<T>.Default.Equals(nestedValue, directValue);
                return new CommandOptionPickupDecision<T>(true, nestedValue, CommandOptionPickupSource.NestedPickup, true, disagreement);
            }

            if (directAvailable)
            {
                return new CommandOptionPickupDecision<T>(true, directValue, CommandOptionPickupSource.DirectCompatibilityFallback, false, false);
            }

            return new CommandOptionPickupDecision<T>(false, default!, CommandOptionPickupSource.None, false, false);
        }

        public static string SourceToken(CommandOptionPickupSource source)
        {
            switch (source)
            {
                case CommandOptionPickupSource.NestedPickup: return AuthoritativeSourceToken;
                case CommandOptionPickupSource.DirectCompatibilityFallback: return FallbackSourceToken;
                default: return UnresolvedSourceToken;
            }
        }
    }
}
