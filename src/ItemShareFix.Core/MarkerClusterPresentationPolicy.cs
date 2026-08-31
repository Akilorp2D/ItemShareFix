using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ItemShareFix.Core
{
    public enum MarkerPresentationMode
    {
        Detailed,
        Compact,
    }

    public enum MarkerCompactMixedStyle
    {
        NeutralSingleDiamond,
        CategoryDiamonds,
        CategoryDiamondPyramid,
    }

    public enum MarkerCountRenderSource
    {
        None,
        TotalCountText,
        CategorySubcounts,
        DetailedCategoryRows,
    }

    /// <summary>
    /// Presentation-only glyph choice. Lifetime remains orthogonal semantic state; Clock is never a marker class/category.
    /// </summary>
    public enum MarkerPresentationGlyphKind
    {
        Diamond,
        Clock,
    }

    public readonly struct MarkerPresentationSettings
    {
        // Four-argument and historical eight-argument constructors preserve the legacy API contract for external
        // callers. Canonical runtime code uses the nine-argument constructor with explicit category sort order.
        public MarkerPresentationSettings(MarkerPresentationMode mode, bool showDistance, float scale, int detailRows)
            : this(
                mode,
                showDistance,
                scale,
                detailRows,
                showCategoryDiamond: true,
                showTierComposition: true,
                compactShowCount: true,
                compactMixedStyle: MarkerCompactMixedStyle.NeutralSingleDiamond,
                categorySortOrder: MarkerCategorySummaryPolicy.DefaultSortOrder,
                useCategorySummaryPresentation: false)
        {
        }

        public MarkerPresentationSettings(
            MarkerPresentationMode mode,
            bool showDistance,
            float scale,
            int detailRows,
            bool showCategoryDiamond,
            bool showTierComposition,
            bool compactShowCount,
            MarkerCompactMixedStyle compactMixedStyle)
            : this(
                mode,
                showDistance,
                scale,
                detailRows,
                showCategoryDiamond,
                showTierComposition,
                compactShowCount,
                compactMixedStyle,
                MarkerCategorySummaryPolicy.DefaultSortOrder,
                useCategorySummaryPresentation: false)
        {
        }

        public MarkerPresentationSettings(
            MarkerPresentationMode mode,
            bool showDistance,
            float scale,
            int detailRows,
            bool showCategoryDiamond,
            bool showTierComposition,
            bool compactShowCount,
            MarkerCompactMixedStyle compactMixedStyle,
            MarkerCategorySortOrder categorySortOrder)
            : this(
                mode,
                showDistance,
                scale,
                detailRows,
                showCategoryDiamond,
                showTierComposition,
                compactShowCount,
                compactMixedStyle,
                categorySortOrder,
                useCategorySummaryPresentation: true)
        {
        }

        private MarkerPresentationSettings(
            MarkerPresentationMode mode,
            bool showDistance,
            float scale,
            int detailRows,
            bool showCategoryDiamond,
            bool showTierComposition,
            bool compactShowCount,
            MarkerCompactMixedStyle compactMixedStyle,
            MarkerCategorySortOrder categorySortOrder,
            bool useCategorySummaryPresentation)
        {
            Mode = mode;
            ShowDistance = showDistance;
            Scale = MarkerClusterPresentationPolicy.ClampMarkerScale(scale);
            DetailRows = MarkerClusterPresentationPolicy.ClampDetailRows(detailRows);
            ShowCategoryDiamond = showCategoryDiamond;
            ShowTierComposition = showTierComposition;
            CompactShowCount = compactShowCount;
            CompactMixedStyle = compactMixedStyle;
            CategorySortOrder = categorySortOrder;
            UseCategorySummaryPresentation = useCategorySummaryPresentation;
        }

        public MarkerPresentationMode Mode { get; }
        public bool ShowDistance { get; }
        public float Scale { get; }
        public int DetailRows { get; }
        public bool ShowCategoryDiamond { get; }
        public bool ShowTierComposition { get; }
        public bool CompactShowCount { get; }
        public MarkerCompactMixedStyle CompactMixedStyle { get; }
        public MarkerCategorySortOrder CategorySortOrder { get; }
        public bool UseCategorySummaryPresentation { get; }
    }

    public readonly struct MarkerCompactCategoryBadge
    {
        public MarkerCompactCategoryBadge(MarkerSemanticCategory category, int count)
            : this(category, count, MarkerCategorySummaryPolicy.DisplayPriority(category), MarkerLifetimeKind.Permanent, MarkerPresentationGlyphKind.Diamond)
        {
        }

        public MarkerCompactCategoryBadge(MarkerSemanticCategory category, int count, int displayPriority)
            : this(category, count, displayPriority, MarkerLifetimeKind.Permanent, MarkerPresentationGlyphKind.Diamond)
        {
        }

        public MarkerCompactCategoryBadge(
            MarkerSemanticCategory category,
            int count,
            int displayPriority,
            MarkerLifetimeIndicatorSpec lifetimeIndicator)
            : this(
                category,
                count,
                displayPriority,
                lifetimeIndicator.Visible ? MarkerLifetimeKind.Temporary : MarkerLifetimeKind.Permanent,
                lifetimeIndicator.Visible ? MarkerPresentationGlyphKind.Clock : MarkerPresentationGlyphKind.Diamond)
        {
        }

        public MarkerCompactCategoryBadge(
            MarkerSemanticCategory category,
            int count,
            int displayPriority,
            MarkerLifetimeKind lifetime,
            MarkerPresentationGlyphKind glyphKind)
        {
            Category = category;
            Count = Math.Max(0, count);
            DisplayPriority = displayPriority;
            Lifetime = lifetime;
            GlyphKind = glyphKind;
            LifetimeIndicator = MarkerLifetimeIndicatorSpec.Hidden;
        }

        public MarkerSemanticCategory Category { get; }
        public int Count { get; }
        public int DisplayPriority { get; }
        public MarkerLifetimeKind Lifetime { get; }
        public MarkerPresentationGlyphKind GlyphKind { get; }
        // Retained API seam for older callers/tests; current rendering expresses lifetime through GlyphKind instead of metadata clocks.
        public MarkerLifetimeIndicatorSpec LifetimeIndicator { get; }
        public float PyramidVerticalOffsetUnits => MarkerCategorySummaryPolicy.PyramidVerticalOffsetUnits(Category);
    }


    public readonly struct MarkerDetailedItemRow
    {
        public MarkerDetailedItemRow(string itemIdentity, string localizedName, MarkerSemanticCategory category, int count)
            : this(itemIdentity, localizedName, category, count, MarkerLifetimeKind.Permanent, localizedName)
        {
        }

        public MarkerDetailedItemRow(
            string itemIdentity,
            string localizedName,
            MarkerSemanticCategory category,
            int count,
            MarkerLifetimeKind lifetime,
            string displayLabel)
        {
            ItemIdentity = itemIdentity ?? string.Empty;
            LocalizedName = string.IsNullOrWhiteSpace(localizedName) ? "Pickup" : localizedName;
            Category = category;
            Count = Math.Max(0, count);
            Lifetime = lifetime;
            GlyphKind = lifetime == MarkerLifetimeKind.Temporary
                ? MarkerPresentationGlyphKind.Clock
                : MarkerPresentationGlyphKind.Diamond;
            DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? LocalizedName : displayLabel;
            LifetimeIndicator = MarkerLifetimePolicy.BuildIndicator(
                lifetime,
                lifetime == MarkerLifetimeKind.Temporary ? Count : 0,
                lifetime == MarkerLifetimeKind.Mixed ? Count : 0,
                lifetime == MarkerLifetimeKind.Unknown ? Count : 0,
                allowCount: false,
                showSingleCountWhenMixed: false);
        }

        public string ItemIdentity { get; }
        public string LocalizedName { get; }
        public MarkerSemanticCategory Category { get; }
        public int Count { get; }
        public MarkerLifetimeKind Lifetime { get; }
        public MarkerPresentationGlyphKind GlyphKind { get; }
        public string DisplayLabel { get; }
        public MarkerLifetimeIndicatorSpec LifetimeIndicator { get; }
    }

    public sealed class MarkerClusterPresentationPlan
    {
        internal MarkerClusterPresentationPlan(
            string text,
            MarkerCompactCategoryBadge[] compactBadges,
            int totalCount,
            int shownDetailRows,
            int overflowPhysicalCount,
            bool expanded,
            bool neutralMainSemantic,
            MarkerCountRenderSource countRenderSource,
            bool showMainDiamond,
            bool showDetailedCategoryRowDiamonds,
            bool showCompactCategoryDiamonds,
            MarkerSemanticCategory mainCategory)
            : this(text, compactBadges, Array.Empty<MarkerDetailedItemRow>(), totalCount, shownDetailRows, overflowPhysicalCount, expanded, neutralMainSemantic, countRenderSource, showMainDiamond, showDetailedCategoryRowDiamonds, showCompactCategoryDiamonds, mainCategory)
        {
        }

        internal MarkerClusterPresentationPlan(
            string text,
            MarkerCompactCategoryBadge[] compactBadges,
            MarkerDetailedItemRow[] detailedItemRows,
            int totalCount,
            int shownDetailRows,
            int overflowPhysicalCount,
            bool expanded,
            bool neutralMainSemantic,
            MarkerCountRenderSource countRenderSource,
            bool showMainDiamond,
            bool showDetailedCategoryRowDiamonds,
            bool showCompactCategoryDiamonds,
            MarkerSemanticCategory mainCategory)
        {
            Text = text ?? string.Empty;
            CompactBadges = compactBadges ?? Array.Empty<MarkerCompactCategoryBadge>();
            DetailedItemRows = detailedItemRows ?? Array.Empty<MarkerDetailedItemRow>();
            TotalCount = Math.Max(0, totalCount);
            ShownDetailRows = Math.Max(0, shownDetailRows);
            OverflowPhysicalCount = Math.Max(0, overflowPhysicalCount);
            Expanded = expanded;
            NeutralMainSemantic = neutralMainSemantic;
            CountRenderSource = countRenderSource;
            ShowMainDiamond = showMainDiamond;
            ShowDetailedCategoryRowDiamonds = showDetailedCategoryRowDiamonds;
            ShowCompactCategoryDiamonds = showCompactCategoryDiamonds;
            MainCategory = mainCategory;
            LifetimeIndicator = MarkerLifetimeIndicatorSpec.Hidden;
        }

        internal void SetLifetimeIndicator(MarkerLifetimeIndicatorSpec lifetimeIndicator)
            => LifetimeIndicator = lifetimeIndicator;

        public string Text { get; }
        public IReadOnlyList<MarkerCompactCategoryBadge> CompactBadges { get; }
        public IReadOnlyList<MarkerCompactCategoryBadge> CategoryEntries => CompactBadges;
        public IReadOnlyList<MarkerDetailedItemRow> DetailedItemRows { get; }
        public int TotalCount { get; }
        public int ShownDetailRows { get; }
        public int OverflowPhysicalCount { get; }
        public bool Expanded { get; }
        public bool NeutralMainSemantic { get; }
        public MarkerCountRenderSource CountRenderSource { get; }
        public bool ShowMainDiamond { get; }
        public bool ShowDetailedCategoryRowDiamonds { get; }
        public bool ShowCompactCategoryDiamonds { get; }
        public MarkerSemanticCategory MainCategory { get; }
        public MarkerLifetimeIndicatorSpec LifetimeIndicator { get; private set; }
        public bool RenderCategorySubcounts => CountRenderSource == MarkerCountRenderSource.CategorySubcounts;
        public bool RenderTotalCount => CountRenderSource == MarkerCountRenderSource.TotalCountText;
    }

    /// <summary>
    /// Presentation-only formatter. World membership is owned below this layer by MarkerWorldClusterTracker and
    /// MarkerDenseAreaSummaryTracker. Presentation settings never participate in physical or dense membership.
    /// </summary>
    public static class MarkerClusterPresentationPolicy
    {
        public const int TOTAL_COUNT_RENDER_SLOTS_PER_PRESENTATION_NODE = 1;
        public const float MarkerScaleMin = 0.75f;
        public const float MarkerScaleMax = 1.25f;
        public const float MarkerScaleDefault = 1.0f;
        public const int MarkerDetailRowsMin = 1;
        public const int MarkerDetailRowsMax = 12;
        public const int MarkerDetailRowsDefault = 5;
        public const int DetailedCompositionLineCharacterBudget = 48;
        public const int MaxCompositionLines = 2;
        public const int MaxCompactCategoryBadges = 11;
        public const int MaxCompactLifetimePresentationGroups = MaxCompactCategoryBadges * 2;
        public const float DetailedItemLabelMaxWidth1080 = 420f;
        public const float DetailedItemLabelMinWidth1080 = 180f;

        public static float ClampMarkerScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return MarkerScaleDefault;
            if (value < MarkerScaleMin) return MarkerScaleMin;
            return value > MarkerScaleMax ? MarkerScaleMax : value;
        }

        public static int ClampDetailRows(int value)
        {
            if (value < MarkerDetailRowsMin) return MarkerDetailRowsMin;
            return value > MarkerDetailRowsMax ? MarkerDetailRowsMax : value;
        }

        /// <summary>
        /// Presentation-only width budget for ordinary Detailed item/equipment text. It follows the existing
        /// 1920x1080 HUD resolution scale and marker UI scale, while remaining bounded to a conservative fraction
        /// of the current screen so a modded localized name cannot stretch a marker across the display.
        /// </summary>
        public static float BuildDetailedItemLabelWidthLimit(float screenWidth, float screenHeight, float markerScale)
        {
            var safeWidth = float.IsNaN(screenWidth) || float.IsInfinity(screenWidth) || screenWidth <= 0f ? 1920f : screenWidth;
            var safeHeight = float.IsNaN(screenHeight) || float.IsInfinity(screenHeight) || screenHeight <= 0f ? 1080f : screenHeight;
            var resolutionScale = Math.Min(safeWidth / 1920f, safeHeight / 1080f);
            if (resolutionScale <= 0f || float.IsNaN(resolutionScale) || float.IsInfinity(resolutionScale)) resolutionScale = 1f;
            var uiScale = ClampMarkerScale(markerScale);
            var preferred = DetailedItemLabelMaxWidth1080 * resolutionScale * uiScale;
            var minimum = DetailedItemLabelMinWidth1080 * resolutionScale * uiScale;
            var screenBound = safeWidth * 0.34f;
            return (float)Math.Max(minimum, Math.Min(preferred, screenBound));
        }

        public static MarkerClusterPresentationPlan Build(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            bool expanded,
            bool russian)
            => Build(cluster, settings, distanceMeters, expanded, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static MarkerClusterPresentationPlan Build(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            bool expanded,
            MarkerLanguage language)
        {
            if (cluster == null) throw new ArgumentNullException(nameof(cluster));
            MarkerClusterPresentationPlan plan;
            if (settings.UseCategorySummaryPresentation)
            {
                plan = settings.Mode == MarkerPresentationMode.Compact
                    ? BuildCompactCategoryPyramid(cluster, settings, distanceMeters, language)
                    : BuildDetailedCategorySummary(cluster, settings, distanceMeters, language);
            }
            else
            {
                plan = settings.Mode == MarkerPresentationMode.Compact
                    ? BuildCompactLegacy(cluster, settings, distanceMeters, expanded, language)
                    : BuildDetailedLegacy(cluster, settings, distanceMeters, expanded, language);
            }

            if ((settings.Mode == MarkerPresentationMode.Compact && !plan.ShowCompactCategoryDiamonds)
                || (settings.Mode == MarkerPresentationMode.Detailed
                    && plan.DetailedItemRows.Count == 0
                    && !plan.ShowDetailedCategoryRowDiamonds))
            {
                plan.SetLifetimeIndicator(BuildClusterLifetimeIndicator(
                    cluster,
                    allowCount: settings.Mode == MarkerPresentationMode.Compact && settings.CompactShowCount,
                    showSingleCountWhenMixed: true));
            }
            return plan;
        }

        public static MarkerClusterPresentationPlan BuildOffscreen(
            int nearestDistanceMeters,
            int sectorTotalCount,
            bool showDistance,
            bool showTotalCount,
            bool russian)
            => BuildOffscreen(
                nearestDistanceMeters,
                sectorTotalCount,
                showDistance,
                showTotalCount,
                MarkerTextLocalization.FromRussianCompatibility(russian),
                MarkerLifetimeKind.Permanent,
                0,
                0,
                0);

        public static MarkerClusterPresentationPlan BuildOffscreen(
            int nearestDistanceMeters,
            int sectorTotalCount,
            bool showDistance,
            bool showTotalCount,
            MarkerLanguage language)
            => BuildOffscreen(
                nearestDistanceMeters,
                sectorTotalCount,
                showDistance,
                showTotalCount,
                language,
                MarkerLifetimeKind.Permanent,
                0,
                0,
                0);

        public static MarkerClusterPresentationPlan BuildOffscreen(
            int nearestDistanceMeters,
            int sectorTotalCount,
            bool showDistance,
            bool showTotalCount,
            bool russian,
            MarkerLifetimeKind lifetime,
            int temporaryPhysicalMembers,
            int mixedLifetimeMembers,
            int unknownLifetimeMembers)
            => BuildOffscreen(
                nearestDistanceMeters, sectorTotalCount, showDistance, showTotalCount,
                MarkerTextLocalization.FromRussianCompatibility(russian), lifetime,
                temporaryPhysicalMembers, mixedLifetimeMembers, unknownLifetimeMembers);

        public static MarkerClusterPresentationPlan BuildOffscreen(
            int nearestDistanceMeters,
            int sectorTotalCount,
            bool showDistance,
            bool showTotalCount,
            MarkerLanguage language,
            MarkerLifetimeKind lifetime,
            int temporaryPhysicalMembers,
            int mixedLifetimeMembers,
            int unknownLifetimeMembers)
        {
            var builder = new StringBuilder(32);
            var source = MarkerCountRenderSource.None;
            if (showTotalCount)
            {
                builder.Append('×').Append(Math.Max(0, sectorTotalCount).ToString(CultureInfo.InvariantCulture));
                source = MarkerCountRenderSource.TotalCountText;
            }
            if (showDistance)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(Math.Max(0, nearestDistanceMeters).ToString(CultureInfo.InvariantCulture)).Append(" ").Append(MarkerTextLocalization.DistanceUnit(language));
            }
            var plan = new MarkerClusterPresentationPlan(
                builder.ToString(),
                Array.Empty<MarkerCompactCategoryBadge>(),
                Math.Max(0, sectorTotalCount),
                0,
                0,
                false,
                true,
                source,
                true,
                false,
                false,
                MarkerSemanticCategory.Unknown);
            // Offscreen lifetime truth: a directional marker may claim Temporary only when the exact
            // represented cluster is Temporary-only. Mixed/Unknown/Permanent remain directionally useful but
            // must not carry a temporary-only clock/count. Aggregated sectors already pass Unknown here.
            plan.SetLifetimeIndicator(lifetime == MarkerLifetimeKind.Temporary
                ? MarkerLifetimePolicy.BuildIndicator(
                    MarkerLifetimeKind.Temporary,
                    temporaryPhysicalMembers,
                    mixedLifetimeMembers: 0,
                    unknownLifetimeMembers: 0,
                    allowCount: showTotalCount,
                    showSingleCountWhenMixed: false)
                : MarkerLifetimeIndicatorSpec.Hidden);
            return plan;
        }

        public static string LocalizedCategoryPlural(MarkerSemanticCategory category, bool russian)
            => LocalizedCategoryPlural(category, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static string LocalizedCategoryPlural(MarkerSemanticCategory category, MarkerLanguage language)
            => MarkerTextLocalization.CategoryPlural(category, language);

        public static string LocalizedCompositionWord(MarkerSemanticCategory category, bool russian)
            => LocalizedCompositionWord(category, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static string LocalizedCompositionWord(MarkerSemanticCategory category, MarkerLanguage language)
            => MarkerTextLocalization.CompositionLabel(category, language);

        public static int CompositionCountSum(IReadOnlyList<MarkerCategoryCount> composition)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            var sum = 0;
            for (var i = 0; i < composition.Count; i++) sum += Math.Max(0, composition[i].Count);
            return sum;
        }

        public static string LocalizedCategorySummaryLabel(MarkerSemanticCategory category, bool russian)
            => LocalizedCategorySummaryLabel(category, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static string LocalizedCategorySummaryLabel(MarkerSemanticCategory category, MarkerLanguage language)
            => MarkerTextLocalization.CategorySummaryLabel(category, language);

        private static MarkerClusterPresentationPlan BuildDetailedCategorySummary(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            MarkerLanguage language)
        {
            return IsCommandChoiceCluster(cluster)
                ? BuildDetailedCommandCategorySummary(cluster, settings, distanceMeters, language)
                : BuildDetailedOrdinaryItemSummary(cluster, settings, distanceMeters, language);
        }

        private static MarkerClusterPresentationPlan BuildDetailedCommandCategorySummary(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            MarkerLanguage language)
        {
            var builder = new StringBuilder(160);
            var total = cluster.TotalCount;
            var neutral = cluster.IsMixedCategory || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown || cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState;
            var mainCategory = neutral ? MarkerSemanticCategory.Unknown : cluster.HomogeneousCategory;
            var entries = BuildCategoryEntriesWithLifetime(cluster, settings.CategorySortOrder);

            if (total <= 1 && IsTruthfulConcreteSingleAggregate(cluster))
            {
                builder.Append(cluster.ItemRows[0].LocalizedName).Append(" ×").Append(total.ToString(CultureInfo.InvariantCulture));
                AppendDistance(builder, settings.ShowDistance, distanceMeters, language);
                return new MarkerClusterPresentationPlan(builder.ToString(), entries, total, 0, 0, false, neutral,
                    MarkerCountRenderSource.TotalCountText, settings.ShowCategoryDiamond, false, false, mainCategory);
            }

            for (var i = 0; i < entries.Length; i++)
            {
                if (i > 0) builder.Append('\n');
                builder.Append(LocalizedCategorySummaryLabel(entries[i].Category, language))
                    .Append(" ×").Append(entries[i].Count.ToString(CultureInfo.InvariantCulture));
            }
            AppendDistance(builder, settings.ShowDistance, distanceMeters, language);
            return new MarkerClusterPresentationPlan(builder.ToString(), entries, total, 0, 0, false, neutral,
                MarkerCountRenderSource.DetailedCategoryRows, false, settings.ShowCategoryDiamond, false, mainCategory);
        }

        private static MarkerClusterPresentationPlan BuildDetailedOrdinaryItemSummary(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            MarkerLanguage language)
        {
            var rows = new List<MarkerDetailedItemRow>(cluster.ItemRows.Count);
            for (var i = 0; i < cluster.ItemRows.Count; i++)
            {
                var source = cluster.ItemRows[i];
                if (source.Count <= 0) continue;
                rows.Add(new MarkerDetailedItemRow(
                    source.ItemIdentity,
                    source.LocalizedName,
                    source.Category,
                    source.Count,
                    source.Lifetime,
                    MarkerLifetimePolicy.BuildDetailedItemDisplayName(string.IsNullOrWhiteSpace(source.LocalizedName) ? MarkerTextLocalization.FallbackPickup(language) : source.LocalizedName, source.Lifetime, language == MarkerLanguage.Russian)));
            }
            rows.Sort((left, right) => CompareDetailedItems(left, right, settings.CategorySortOrder));

            var visible = Math.Min(settings.DetailRows, rows.Count);
            var overflowTypes = Math.Max(0, rows.Count - visible);
            var builder = new StringBuilder(192);
            for (var i = 0; i < visible; i++)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(rows[i].DisplayLabel).Append(" ×").Append(rows[i].Count.ToString(CultureInfo.InvariantCulture));
            }
            if (overflowTypes > 0)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(BuildDetailedOverflowText(overflowTypes, language));
            }
            AppendDistance(builder, settings.ShowDistance, distanceMeters, language);

            var visibleRows = new MarkerDetailedItemRow[visible];
            for (var i = 0; i < visible; i++) visibleRows[i] = rows[i];
            var neutral = cluster.IsMixedCategory || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown;
            var mainCategory = neutral ? MarkerSemanticCategory.Unknown : cluster.HomogeneousCategory;
            return new MarkerClusterPresentationPlan(
                builder.ToString(), Array.Empty<MarkerCompactCategoryBadge>(), visibleRows, cluster.TotalCount, visible, overflowTypes,
                false, neutral, MarkerCountRenderSource.None, false, false, false, mainCategory);
        }

        public static string BuildDetailedOverflowText(int hiddenDistinctTypes, bool russian)
            => BuildDetailedOverflowText(hiddenDistinctTypes, MarkerTextLocalization.FromRussianCompatibility(russian));

        public static string BuildDetailedOverflowText(int hiddenDistinctTypes, MarkerLanguage language)
            => MarkerTextLocalization.FormatMoreTypes(hiddenDistinctTypes, language);

        public static string RussianDistinctTypeWord(int hiddenDistinctTypes)
            => MarkerTextLocalization.RussianDistinctTypeWord(hiddenDistinctTypes);

        private static int CompareDetailedItems(MarkerDetailedItemRow left, MarkerDetailedItemRow right, MarkerCategorySortOrder sortOrder)
        {
            var leftPriority = MarkerCategorySummaryPolicy.DisplayPriority(left.Category);
            var rightPriority = MarkerCategorySummaryPolicy.DisplayPriority(right.Category);
            var category = rightPriority.CompareTo(leftPriority);
            if (sortOrder == MarkerCategorySortOrder.LowToHigh) category = -category;
            if (category != 0) return category;
            var name = string.Compare(left.LocalizedName, right.LocalizedName, StringComparison.Ordinal);
            if (name != 0) return name;
            var identity = string.Compare(left.ItemIdentity, right.ItemIdentity, StringComparison.Ordinal);
            if (identity != 0) return identity;
            return MarkerLifetimePolicy.DetailedSortRank(left.Lifetime).CompareTo(MarkerLifetimePolicy.DetailedSortRank(right.Lifetime));
        }

        private static bool IsCommandChoiceCluster(MarkerSemanticCluster cluster)
        {
            if (cluster.ItemRows.Count == 0) return cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState;
            for (var i = 0; i < cluster.ItemRows.Count; i++)
                if (!(cluster.ItemRows[i].ItemIdentity ?? string.Empty).StartsWith("COMMAND:", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static MarkerClusterPresentationPlan BuildCompactCategoryPyramid(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            MarkerLanguage language)
        {
            var total = cluster.TotalCount;
            var entries = BuildCategoryEntriesWithLifetime(cluster, settings.CategorySortOrder);
            var neutral = cluster.IsMixedCategory || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown || cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState;
            var mainCategory = neutral ? MarkerSemanticCategory.Unknown : cluster.HomogeneousCategory;
            var builder = new StringBuilder(48);

            AppendDistance(builder, settings.ShowDistance, distanceMeters, language);
            return new MarkerClusterPresentationPlan(
                builder.ToString(), entries, total, 0, 0, false, neutral,
                settings.CompactShowCount ? MarkerCountRenderSource.CategorySubcounts : MarkerCountRenderSource.None,
                false, false, true, mainCategory);
        }

        private static MarkerClusterPresentationPlan BuildDetailedLegacy(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            bool expanded,
            MarkerLanguage language)
        {
            var builder = new StringBuilder(192);
            var total = cluster.TotalCount;
            var neutral = cluster.IsMixedCategory || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown || cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState;
            var mainCategory = neutral ? MarkerSemanticCategory.Unknown : cluster.HomogeneousCategory;

            if (IsTruthfulConcreteSingleAggregate(cluster))
            {
                builder.Append(cluster.ItemRows[0].LocalizedName)
                    .Append(" ×").Append(total.ToString(CultureInfo.InvariantCulture));
            }
            else if (cluster.IsHomogeneousCategory)
            {
                builder.Append(LocalizedCategoryPlural(cluster.HomogeneousCategory, language))
                    .Append(" ×").Append(total.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(MarkerTextLocalization.GenericItems(language)).Append(" ×").Append(total.ToString(CultureInfo.InvariantCulture));
                if (settings.ShowTierComposition) AppendDetailedComposition(builder, cluster.Composition, language);
            }

            var shownRows = 0;
            var overflow = 0;
            if (expanded && total > 1)
            {
                AppendExpandedDetailRows(builder, cluster.ItemRows, settings.DetailRows, total, language, out shownRows, out overflow);
            }

            AppendDistance(builder, settings.ShowDistance, distanceMeters, language);
            return new MarkerClusterPresentationPlan(
                builder.ToString(),
                Array.Empty<MarkerCompactCategoryBadge>(),
                total,
                shownRows,
                overflow,
                expanded,
                neutral,
                MarkerCountRenderSource.TotalCountText,
                settings.ShowCategoryDiamond,
                false,
                false,
                mainCategory);
        }

        private static MarkerClusterPresentationPlan BuildCompactLegacy(
            MarkerSemanticCluster cluster,
            MarkerPresentationSettings settings,
            int distanceMeters,
            bool expanded,
            MarkerLanguage language)
        {
            var total = cluster.TotalCount;
            // Retain semantic badge metadata for legacy callers. Rendering uses CountRenderSource to decide whether
            // those counts are actually rendered, eliminating the old duplicate-count path structurally.
            var badges = BuildCategoryEntriesWithLifetime(cluster, MarkerCategorySortOrder.HighToLow, preserveCompositionOrder: true);

            var categoryDiamonds = cluster.IsMixedCategory && settings.CompactMixedStyle == MarkerCompactMixedStyle.CategoryDiamonds;
            var source = categoryDiamonds
                ? settings.CompactShowCount ? MarkerCountRenderSource.CategorySubcounts : MarkerCountRenderSource.None
                : settings.CompactShowCount ? MarkerCountRenderSource.TotalCountText : MarkerCountRenderSource.None;
            var builder = new StringBuilder(160);
            if (source == MarkerCountRenderSource.TotalCountText)
                builder.Append('×').Append(total.ToString(CultureInfo.InvariantCulture));

            var shownRows = 0;
            var overflow = 0;
            if (expanded)
            {
                AppendExpandedDetailRows(builder, cluster.ItemRows, settings.DetailRows, total, language, out shownRows, out overflow);
            }

            AppendDistance(builder, settings.ShowDistance, distanceMeters, language);
            var neutral = cluster.IsMixedCategory || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown || cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState;
            var mainCategory = neutral ? MarkerSemanticCategory.Unknown : cluster.HomogeneousCategory;
            return new MarkerClusterPresentationPlan(
                builder.ToString(),
                badges,
                total,
                shownRows,
                overflow,
                expanded,
                neutral,
                source,
                !categoryDiamonds,
                false,
                categoryDiamonds,
                mainCategory);
        }

        private static bool IsTruthfulConcreteSingleAggregate(MarkerSemanticCluster cluster)
        {
            if (cluster.ItemRows.Count != 1 || !cluster.IsHomogeneousCategory) return false;
            if (cluster.HomogeneousCategory == MarkerSemanticCategory.CommandState || cluster.HomogeneousCategory == MarkerSemanticCategory.Unknown) return false;
            var identity = cluster.ItemRows[0].ItemIdentity ?? string.Empty;
            return !identity.StartsWith("COMMAND:", StringComparison.OrdinalIgnoreCase)
                && !identity.StartsWith("UNKNOWN:", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendExpandedDetailRows(
            StringBuilder builder,
            IReadOnlyList<MarkerItemAggregate> itemRows,
            int requestedRows,
            int totalPhysicalCount,
            MarkerLanguage language,
            out int shownRows,
            out int overflowPhysicalCount)
        {
            var maxRows = Math.Min(ClampDetailRows(requestedRows), MarkerDetailRowsMax);
            Span<int> selectedIndices = stackalloc int[MarkerDetailRowsMax];
            for (var i = 0; i < selectedIndices.Length; i++) selectedIndices[i] = -1;

            var selectedCount = 0;
            for (var itemIndex = 0; itemIndex < itemRows.Count; itemIndex++)
            {
                var candidate = itemRows[itemIndex];
                if (candidate.Count <= 0) continue;

                var insertAt = selectedCount;
                for (var selectedIndex = 0; selectedIndex < selectedCount; selectedIndex++)
                {
                    if (CompareExpandedDetailPriority(candidate, itemRows[selectedIndices[selectedIndex]]) < 0)
                    {
                        insertAt = selectedIndex;
                        break;
                    }
                }

                if (insertAt >= maxRows) continue;
                if (selectedCount < maxRows) selectedCount++;
                for (var shift = selectedCount - 1; shift > insertAt; shift--)
                    selectedIndices[shift] = selectedIndices[shift - 1];
                selectedIndices[insertAt] = itemIndex;
            }

            var represented = 0;
            for (var i = 0; i < selectedCount; i++)
            {
                var row = itemRows[selectedIndices[i]];
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(row.LocalizedName);
                if (row.Count > 1) builder.Append(" ×").Append(row.Count.ToString(CultureInfo.InvariantCulture));
                represented += row.Count;
            }

            shownRows = selectedCount;
            overflowPhysicalCount = Math.Max(0, totalPhysicalCount - represented);
            if (overflowPhysicalCount > 0)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(MarkerTextLocalization.FormatPhysicalItems(overflowPhysicalCount, language));
            }
        }

        private static int CompareExpandedDetailPriority(MarkerItemAggregate left, MarkerItemAggregate right)
        {
            var count = right.Count.CompareTo(left.Count);
            if (count != 0) return count;

            var category = MarkerSemanticCategoryPolicy.StableOrder(left.Category)
                .CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category));
            if (category != 0) return category;

            var name = string.Compare(left.LocalizedName, right.LocalizedName, StringComparison.Ordinal);
            if (name != 0) return name;

            return string.Compare(left.ItemIdentity, right.ItemIdentity, StringComparison.Ordinal);
        }

        private static void AppendDetailedComposition(StringBuilder builder, IReadOnlyList<MarkerCategoryCount> composition, MarkerLanguage language)
        {
            var lineLength = 0;
            var lineCount = 0;
            for (var i = 0; i < composition.Count; i++)
            {
                var entry = composition[i];
                if (entry.Count <= 0) continue;
                var token = LocalizedCompositionWord(entry.Category, language) + " " + entry.Count.ToString(CultureInfo.InvariantCulture);
                var separatorLength = lineLength == 0 ? 0 : 3;
                if (lineLength > 0 && lineLength + separatorLength + token.Length > DetailedCompositionLineCharacterBudget && lineCount + 1 < MaxCompositionLines)
                {
                    builder.Append('\n');
                    lineLength = 0;
                    lineCount++;
                    separatorLength = 0;
                }
                else if (lineLength == 0)
                {
                    builder.Append('\n');
                }

                if (lineLength > 0) builder.Append(" · ");
                builder.Append(token);
                lineLength += separatorLength + token.Length;
            }
        }

        private static MarkerCompactCategoryBadge[] BuildCategoryEntriesWithLifetime(
            MarkerSemanticCluster cluster,
            MarkerCategorySortOrder sortOrder,
            bool preserveCompositionOrder = false)
        {
            MarkerCompactCategoryBadge[] categories;
            if (preserveCompositionOrder)
            {
                var nonZero = 0;
                for (var i = 0; i < cluster.Composition.Count && nonZero < MaxCompactCategoryBadges; i++)
                    if (cluster.Composition[i].Count > 0) nonZero++;

                categories = new MarkerCompactCategoryBadge[nonZero];
                var write = 0;
                for (var i = 0; i < cluster.Composition.Count && write < categories.Length; i++)
                {
                    var source = cluster.Composition[i];
                    if (source.Count <= 0) continue;
                    categories[write++] = new MarkerCompactCategoryBadge(
                        source.Category,
                        source.Count,
                        MarkerCategorySummaryPolicy.DisplayPriority(source.Category));
                }
            }
            else
            {
                categories = MarkerCategorySummaryPolicy.BuildCategoryEntries(cluster.Composition, sortOrder);
            }
            if (categories.Length == 0) return categories;

            var result = new List<MarkerCompactCategoryBadge>(Math.Min(MaxCompactLifetimePresentationGroups, categories.Length * 2));
            for (var categoryIndex = 0; categoryIndex < categories.Length && result.Count < MaxCompactLifetimePresentationGroups; categoryIndex++)
            {
                var category = categories[categoryIndex];
                var temporary = 0;
                var permanent = 0;
                var unresolved = 0;
                for (var rowIndex = 0; rowIndex < cluster.ItemRows.Count; rowIndex++)
                {
                    var row = cluster.ItemRows[rowIndex];
                    if (row.Category != category.Category || row.Count <= 0) continue;
                    switch (row.Lifetime)
                    {
                        case MarkerLifetimeKind.Temporary: temporary += row.Count; break;
                        case MarkerLifetimeKind.Permanent: permanent += row.Count; break;
                        default: unresolved += row.Count; break;
                    }
                }

                // Unknown/Mixed lifetime is deliberately not guessed as temporary. It stays on the safe diamond path.
                var diamondCount = Math.Max(0, permanent + unresolved);
                if (diamondCount > 0 && result.Count < MaxCompactLifetimePresentationGroups)
                {
                    var diamondLifetime = unresolved == 0
                        ? MarkerLifetimeKind.Permanent
                        : MarkerLifetimeKind.Unknown;
                    result.Add(new MarkerCompactCategoryBadge(
                        category.Category,
                        diamondCount,
                        category.DisplayPriority,
                        diamondLifetime,
                        MarkerPresentationGlyphKind.Diamond));
                }

                if (temporary > 0 && result.Count < MaxCompactLifetimePresentationGroups)
                {
                    result.Add(new MarkerCompactCategoryBadge(
                        category.Category,
                        temporary,
                        category.DisplayPriority,
                        MarkerLifetimeKind.Temporary,
                        MarkerPresentationGlyphKind.Clock));
                }
            }
            return result.ToArray();
        }


        private static MarkerLifetimeIndicatorSpec BuildClusterLifetimeIndicator(
            MarkerSemanticCluster cluster,
            bool allowCount,
            bool showSingleCountWhenMixed)
            => MarkerLifetimePolicy.BuildIndicator(
                cluster.LifetimeSummary,
                cluster.TemporaryPhysicalMemberCount,
                cluster.MixedLifetimeMemberCount,
                cluster.UnknownLifetimeMemberCount,
                allowCount,
                showSingleCountWhenMixed);

        private static void AppendLifetimeSummary(
            StringBuilder builder,
            MarkerSemanticCluster cluster,
            bool showTemporaryCount,
            bool russian)
        {
            var line = MarkerLifetimePolicy.BuildSummaryLine(
                cluster.LifetimeSummary,
                cluster.TemporaryPhysicalMemberCount,
                showTemporaryCount,
                russian);
            if (string.IsNullOrEmpty(line)) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(line);
        }

        private static void AppendDistance(StringBuilder builder, bool showDistance, int distanceMeters, MarkerLanguage language)
        {
            if (!showDistance) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(Math.Max(0, distanceMeters).ToString(CultureInfo.InvariantCulture)).Append(" ").Append(MarkerTextLocalization.DistanceUnit(language));
        }
    }
}
