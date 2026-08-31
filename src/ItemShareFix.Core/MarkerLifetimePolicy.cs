using System;
using System.Globalization;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Orthogonal marker lifetime semantic. Lifetime is presentation metadata only; it is not a marker class,
    /// category, physical-cluster membership key, or gameplay/shareability policy.
    /// </summary>
    public enum MarkerLifetimeKind
    {
        Permanent,
        Temporary,
        Mixed,
        Unknown,
    }

    /// <summary>
    /// Explicit composite key used only for semantic item-row aggregation. The underlying ItemIdentity is retained
    /// unchanged so icon/color/category resolution continues to use the original item semantic key.
    /// </summary>
    public readonly struct MarkerItemLifetimeKey : IEquatable<MarkerItemLifetimeKey>
    {
        public MarkerItemLifetimeKey(string itemIdentity, MarkerLifetimeKind lifetime)
        {
            ItemIdentity = itemIdentity ?? string.Empty;
            Lifetime = lifetime;
        }

        public string ItemIdentity { get; }
        public MarkerLifetimeKind Lifetime { get; }

        public bool Equals(MarkerItemLifetimeKey other)
            => Lifetime == other.Lifetime && string.Equals(ItemIdentity, other.ItemIdentity, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is MarkerItemLifetimeKey other && Equals(other);

        public override int GetHashCode()
            => ((ItemIdentity != null ? StringComparer.Ordinal.GetHashCode(ItemIdentity) : 0) * 397) ^ (int)Lifetime;
    }

    /// <summary>
    /// Structured marker-only lifetime indicator state. It deliberately carries no localized lifetime word and no
    /// time-left value. A count is exposed only when exact lifetime membership makes it truthful.
    /// </summary>
    public readonly struct MarkerLifetimeIndicatorSpec
    {
        public MarkerLifetimeIndicatorSpec(bool visible, int temporaryCount, bool countKnown, bool showCount)
        {
            Visible = visible;
            TemporaryCount = Math.Max(0, temporaryCount);
            CountKnown = countKnown;
            ShowCount = visible && countKnown && showCount && TemporaryCount > 0;
        }

        public bool Visible { get; }
        public int TemporaryCount { get; }
        public bool CountKnown { get; }
        public bool ShowCount { get; }
        public string CountText => ShowCount
            ? "×" + TemporaryCount.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        public static MarkerLifetimeIndicatorSpec Hidden => new MarkerLifetimeIndicatorSpec(false, 0, false, false);
    }

    public static class MarkerLifetimePolicy
    {
        public static MarkerLifetimeKind FromTemporaryFlag(bool isTemporary)
            => isTemporary ? MarkerLifetimeKind.Temporary : MarkerLifetimeKind.Permanent;

        /// <summary>
        /// Presentation-only eligibility gate for temporary markers. Disabling temporary sharing suppresses
        /// exact Temporary marker candidates while preserving Permanent, Mixed, and Unknown candidates.
        /// </summary>
        public static bool IsMarkerEligible(MarkerLifetimeKind lifetime, bool shareTemporaryItems)
            => shareTemporaryItems || lifetime != MarkerLifetimeKind.Temporary;

        public static MarkerLifetimeKind FromExactOptionKinds(bool sawTemporary, bool sawPermanent)
        {
            if (sawTemporary && sawPermanent) return MarkerLifetimeKind.Mixed;
            if (sawTemporary) return MarkerLifetimeKind.Temporary;
            if (sawPermanent) return MarkerLifetimeKind.Permanent;
            return MarkerLifetimeKind.Unknown;
        }

        public static MarkerLifetimeKind SummarizeCluster(
            int totalPhysicalMembers,
            int temporaryPhysicalMembers,
            int mixedLifetimeMembers,
            int unknownLifetimeMembers)
        {
            var total = Math.Max(0, totalPhysicalMembers);
            var temporary = Math.Max(0, temporaryPhysicalMembers);
            var mixed = Math.Max(0, mixedLifetimeMembers);
            var unknown = Math.Max(0, unknownLifetimeMembers);
            var permanent = Math.Max(0, total - temporary - mixed - unknown);

            if (mixed > 0 || (temporary > 0 && permanent > 0)) return MarkerLifetimeKind.Mixed;
            if (temporary > 0) return MarkerLifetimeKind.Temporary;
            if (unknown > 0) return MarkerLifetimeKind.Unknown;
            return MarkerLifetimeKind.Permanent;
        }

        public static MarkerLifetimeIndicatorSpec BuildIndicator(
            MarkerLifetimeKind lifetime,
            int temporaryPhysicalMembers,
            int mixedLifetimeMembers,
            int unknownLifetimeMembers,
            bool allowCount,
            bool showSingleCountWhenMixed)
        {
            var visible = lifetime == MarkerLifetimeKind.Temporary || lifetime == MarkerLifetimeKind.Mixed;
            if (!visible) return MarkerLifetimeIndicatorSpec.Hidden;

            var temporary = Math.Max(0, temporaryPhysicalMembers);
            var countKnown = Math.Max(0, mixedLifetimeMembers) == 0 && Math.Max(0, unknownLifetimeMembers) == 0;
            var showCount = allowCount
                && countKnown
                && temporary > 0
                && (temporary > 1 || (showSingleCountWhenMixed && lifetime == MarkerLifetimeKind.Mixed));
            return new MarkerLifetimeIndicatorSpec(true, temporary, countKnown, showCount);
        }

        /// <summary>
        /// Retained API seam for inherited callers. Lifetime is represented by glyphs rather than visible item-label words;
        /// lifetime is now rendered by structured uGUI indicator metadata.
        /// </summary>
        public static string BuildDetailedItemDisplayName(string localizedName, MarkerLifetimeKind lifetime, bool russian)
        {
            _ = lifetime;
            _ = russian;
            return string.IsNullOrWhiteSpace(localizedName) ? "Pickup" : localizedName;
        }

        /// <summary>
        /// Retained API seam for inherited callers. Visible lifetime text bands are intentionally not rendered.
        /// </summary>
        public static string BuildSummaryLine(
            MarkerLifetimeKind lifetime,
            int temporaryPhysicalMembers,
            bool showTemporaryCount,
            bool russian)
        {
            _ = lifetime;
            _ = temporaryPhysicalMembers;
            _ = showTemporaryCount;
            _ = russian;
            return string.Empty;
        }

        public static int DetailedSortRank(MarkerLifetimeKind lifetime)
        {
            switch (lifetime)
            {
                case MarkerLifetimeKind.Temporary: return 0;
                case MarkerLifetimeKind.Permanent: return 1;
                case MarkerLifetimeKind.Mixed: return 2;
                default: return 3;
            }
        }
    }
}
