using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public enum MarkerCategorySortOrder
    {
        HighToLow,
        LowToHigh,
    }


    public readonly struct MarkerCompactLayoutSlot
    {
        public MarkerCompactLayoutSlot(int row, int column, int rowSize, float xUnits, float yUnits)
        {
            Row = row; Column = column; RowSize = rowSize; XUnits = xUnits; YUnits = yUnits;
        }
        public int Row { get; }
        public int Column { get; }
        public int RowSize { get; }
        public float XUnits { get; }
        public float YUnits { get; }
    }


    public readonly struct MarkerCompactCellGeometry
    {
        public MarkerCompactCellGeometry(float badgeSize, float countWidth, float countGap, float cellWidth, float horizontalStride, float verticalStride)
        {
            BadgeSize = badgeSize; CountWidth = countWidth; CountGap = countGap; CellWidth = cellWidth; HorizontalStride = horizontalStride; VerticalStride = verticalStride;
        }
        public float BadgeSize { get; }
        public float CountWidth { get; }
        public float CountGap { get; }
        public float CellWidth { get; }
        public float HorizontalStride { get; }
        public float VerticalStride { get; }
    }

    /// <summary>
    /// Canonical category-summary ordering/layout policy. This is presentation-only: it accepts semantic
    /// composition that already exists on the current presentation node and never participates in world membership.
    /// The priority table is a deterministic display priority, not an economic/gameplay value claim.
    /// </summary>
    public static class MarkerCategorySummaryPolicy
    {
        public const MarkerCategorySortOrder DefaultSortOrder = MarkerCategorySortOrder.HighToLow;
        public const float PyramidMaxVerticalOffsetUnits = 0.60f;
        public const float PyramidMinimumHorizontalSpacingFactor = 0.62f;
        public const int MaxCategories = 11;

        public static int DisplayPriority(MarkerSemanticCategory category)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier3: return 110;
                case MarkerSemanticCategory.Boss: return 100;
                case MarkerSemanticCategory.Lunar: return 90;
                case MarkerSemanticCategory.Void: return 80;
                case MarkerSemanticCategory.Tier2: return 70;
                case MarkerSemanticCategory.Equipment: return 60;
                case MarkerSemanticCategory.LunarEquipment: return 50;
                case MarkerSemanticCategory.Tier1: return 40;
                case MarkerSemanticCategory.CommandState: return 30;
                case MarkerSemanticCategory.Other: return 20;
                default: return 10;
            }
        }

        public static MarkerCompactCategoryBadge[] BuildCategoryEntries(
            IReadOnlyList<MarkerCategoryCount> composition,
            MarkerCategorySortOrder sortOrder)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            var nonZero = 0;
            for (var i = 0; i < composition.Count && nonZero < MaxCategories; i++)
                if (composition[i].Count > 0) nonZero++;

            var entries = new MarkerCompactCategoryBadge[nonZero];
            var write = 0;
            for (var i = 0; i < composition.Count && write < entries.Length; i++)
            {
                var source = composition[i];
                if (source.Count <= 0) continue;
                entries[write++] = new MarkerCompactCategoryBadge(source.Category, source.Count, DisplayPriority(source.Category));
            }

            // Bounded insertion sort: semantic categories are capped at eleven, so this avoids an allocation-heavy
            // full LINQ sort in the marker hot path while remaining deterministic.
            for (var i = 1; i < entries.Length; i++)
            {
                var candidate = entries[i];
                var j = i - 1;
                while (j >= 0 && Compare(candidate, entries[j], sortOrder) < 0)
                {
                    entries[j + 1] = entries[j];
                    j--;
                }
                entries[j + 1] = candidate;
            }
            return entries;
        }

        public static float PyramidVerticalOffsetUnits(MarkerSemanticCategory category)
        {
            var priority = DisplayPriority(category);
            const float minimumPriority = 10f;
            const float maximumPriority = 110f;
            var normalized = (priority - minimumPriority) / (maximumPriority - minimumPriority);
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            return normalized * PyramidMaxVerticalOffsetUnits;
        }

        public static float PyramidHorizontalSpacingFactor(int categoryCount)
        {
            if (categoryCount <= 3) return 1f;
            var factor = 1f - (categoryCount - 3) * 0.055f;
            return factor < PyramidMinimumHorizontalSpacingFactor ? PyramidMinimumHorizontalSpacingFactor : factor;
        }

        public static float BuildCategoryGlyphSize(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize)
        {
            var width = IsFinitePositive(screenWidth) ? screenWidth : 1920f;
            var height = IsFinitePositive(screenHeight) ? screenHeight : 1080f;
            var boundedScale = IsFinitePositive(markerScale) ? markerScale : 1f;
            if (boundedScale < 0.75f) boundedScale = 0.75f;
            if (boundedScale > 1.25f) boundedScale = 1.25f;
            var viewportScale = Math.Min(width / 1920f, height / 1080f);
            if (!IsFinitePositive(viewportScale)) viewportScale = 1f;
            var uiScale = viewportScale * boundedScale;
            var safeIndicator = IsFinitePositive(indicatorSize) ? indicatorSize : 24f * uiScale;
            var safeFont = IsFinitePositive(fontSize) ? fontSize : 20f * uiScale;

            // Canonical category-glyph edge size shared by Detailed and Compact presentation. It preserves the accepted Detailed
            // formula and is consumed by both Detailed category rows and Compact category/lifetime groups.
            return Math.Max(12f * uiScale, Math.Min(safeIndicator * 0.58f, safeFont * 0.78f));
        }

        /// <summary>
        /// Canonical spacing between the permanent/temporary glyphs that belong to one semantic
        /// category slot. This is local slot geometry only and must never be used as the outer category stride.
        /// </summary>
        public static float BuildCompactLifetimeGroupGap(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize)
        {
            var width = IsFinitePositive(screenWidth) ? screenWidth : 1920f;
            var height = IsFinitePositive(screenHeight) ? screenHeight : 1080f;
            var boundedScale = IsFinitePositive(markerScale) ? markerScale : 1f;
            if (boundedScale < 0.75f) boundedScale = 0.75f;
            if (boundedScale > 1.25f) boundedScale = 1.25f;
            var viewportScale = Math.Min(width / 1920f, height / 1080f);
            if (!IsFinitePositive(viewportScale)) viewportScale = 1f;
            var uiScale = viewportScale * boundedScale;
            var safeIndicator = IsFinitePositive(indicatorSize) ? indicatorSize : 24f * uiScale;
            var safeFont = IsFinitePositive(fontSize) ? fontSize : 20f * uiScale;
            var glyphSize = BuildCategoryGlyphSize(width, height, boundedScale, safeIndicator, safeFont);
            return Math.Max(4f * uiScale, glyphSize * 0.18f);
        }

        /// <summary>
        /// Outer category-center stride. It is deliberately derived from canonical glyph geometry,
        /// never from the width of an actual mixed-lifetime subgroup. The mixed-safe lower bound keeps a centered
        /// Diamond+Clock pair from colliding with the adjacent category glyph while retaining a tight pyramid.
        /// </summary>
        public static float BuildCompactCategoryCenterHorizontalStride(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize,
            int categoryCount)
        {
            var width = IsFinitePositive(screenWidth) ? screenWidth : 1920f;
            var height = IsFinitePositive(screenHeight) ? screenHeight : 1080f;
            var boundedScale = IsFinitePositive(markerScale) ? markerScale : 1f;
            if (boundedScale < 0.75f) boundedScale = 0.75f;
            if (boundedScale > 1.25f) boundedScale = 1.25f;
            var viewportScale = Math.Min(width / 1920f, height / 1080f);
            if (!IsFinitePositive(viewportScale)) viewportScale = 1f;
            var uiScale = viewportScale * boundedScale;
            var safeIndicator = IsFinitePositive(indicatorSize) ? indicatorSize : 24f * uiScale;
            var safeFont = IsFinitePositive(fontSize) ? fontSize : 20f * uiScale;
            var geometry = BuildCompactCellGeometry(width, height, boundedScale, safeIndicator, safeFont, showCount: false);
            var localLifetimeGap = BuildCompactLifetimeGroupGap(width, height, boundedScale, safeIndicator, safeFont);
            var neighborEdgeGap = Math.Max(4f * uiScale, geometry.BadgeSize * 0.18f);
            var acceptedTopologyStride = geometry.HorizontalStride * PyramidHorizontalSpacingFactor(categoryCount);
            var slots = BuildCompactLayout(categoryCount);
            var hasThreeWideRow = false;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].RowSize >= 3)
                {
                    hasThreeWideRow = true;
                    break;
                }
            }
            if (!hasThreeWideRow) return acceptedTopologyStride;

            // A centered mixed pair in a three-wide row is the only canonical topology that can have a
            // same-row category neighbor on both sides. Use the mathematically minimal fixed stride that
            // preserves a bounded edge gap there; two-wide rows expand mixed pairs outward locally instead.
            var mixedSafeStride = geometry.BadgeSize * 1.5f + localLifetimeGap * 0.5f + neighborEdgeGap;
            return Math.Max(acceptedTopologyStride, mixedSafeStride);
        }

        /// <summary>
        /// Vertical category-center stride retains the accepted category-cell geometry scaled through the
        /// Canonical glyph size. Count text does not redefine semantic category centers.
        /// </summary>
        public static float BuildCompactCategoryCenterVerticalStride(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize)
            => BuildCompactCellGeometry(screenWidth, screenHeight, markerScale, indicatorSize, fontSize, showCount: false).VerticalStride;

        /// <summary>
        /// Detailed is the spacing reference. This returns the visible edge-to-edge gap between a Detailed
        /// category glyph line and the following distance line using the exact line-height formula already consumed
        /// by the Detailed renderer. Compact anchors its lowest actually rendered glyph/count edge to this gap.
        /// </summary>
        public static float BuildDetailedCategoryDistanceGap(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize)
        {
            var width = IsFinitePositive(screenWidth) ? screenWidth : 1920f;
            var height = IsFinitePositive(screenHeight) ? screenHeight : 1080f;
            var boundedScale = IsFinitePositive(markerScale) ? markerScale : 1f;
            if (boundedScale < 0.75f) boundedScale = 0.75f;
            if (boundedScale > 1.25f) boundedScale = 1.25f;
            var viewportScale = Math.Min(width / 1920f, height / 1080f);
            if (!IsFinitePositive(viewportScale)) viewportScale = 1f;
            var uiScale = viewportScale * boundedScale;
            var safeIndicator = IsFinitePositive(indicatorSize) ? indicatorSize : 24f * uiScale;
            var safeFont = IsFinitePositive(fontSize) ? fontSize : 20f * uiScale;
            var glyphSize = BuildCategoryGlyphSize(width, height, boundedScale, safeIndicator, safeFont);
            var detailedTextLineHeight = safeFont * 1.18f;
            var detailedLineStride = Math.Max(detailedTextLineHeight, glyphSize + 2f * uiScale);
            return Math.Max(0f, detailedLineStride - (glyphSize + detailedTextLineHeight) * 0.5f);
        }

        /// <summary>
        /// Local mixed-lifetime placement inside one outer category slot. Edge slots expand outward; a single-slot
        /// or middle slot remains centered. This prevents a local Diamond+Clock pair from moving any outer category
        /// center while avoiding the nearest same-row category glyph.
        /// </summary>
        public static float BuildCompactLifetimeGroupOffsetX(
            MarkerCompactLayoutSlot categorySlot,
            int groupOrdinal,
            int groupCount,
            float glyphSize,
            float lifetimeGroupGap)
        {
            var count = Math.Max(1, groupCount);
            var ordinal = Math.Max(0, Math.Min(count - 1, groupOrdinal));
            if (count == 1) return 0f;
            var safeGlyph = IsFinitePositive(glyphSize) ? glyphSize : 1f;
            var safeGap = !float.IsNaN(lifetimeGroupGap) && !float.IsInfinity(lifetimeGroupGap) && lifetimeGroupGap > 0f
                ? lifetimeGroupGap
                : 0f;
            var separation = safeGlyph + safeGap;
            var rowMiddle = (Math.Max(1, categorySlot.RowSize) - 1) * 0.5f;
            if (categorySlot.RowSize > 1 && categorySlot.Column < rowMiddle)
                return -ordinal * separation;
            if (categorySlot.RowSize > 1 && categorySlot.Column > rowMiddle)
                return ordinal * separation;
            return (ordinal - (count - 1) * 0.5f) * separation;
        }

        public static MarkerCompactCellGeometry BuildCompactCellGeometry(
            float screenWidth,
            float screenHeight,
            float markerScale,
            float indicatorSize,
            float fontSize,
            bool showCount)
        {
            var width = IsFinitePositive(screenWidth) ? screenWidth : 1920f;
            var height = IsFinitePositive(screenHeight) ? screenHeight : 1080f;
            var boundedScale = IsFinitePositive(markerScale) ? markerScale : 1f;
            if (boundedScale < 0.75f) boundedScale = 0.75f;
            if (boundedScale > 1.25f) boundedScale = 1.25f;
            var viewportScale = Math.Min(width / 1920f, height / 1080f);
            if (!IsFinitePositive(viewportScale)) viewportScale = 1f;
            var uiScale = viewportScale * boundedScale;
            var safeIndicator = IsFinitePositive(indicatorSize) ? indicatorSize : 24f * uiScale;
            var safeFont = IsFinitePositive(fontSize) ? fontSize : 20f * uiScale;
            var badgeSize = BuildCategoryGlyphSize(width, height, boundedScale, safeIndicator, safeFont);
            var countWidth = showCount ? Math.Max(24f * uiScale, safeFont * 2.20f) : 0f;
            var countGap = showCount ? Math.Max(5f * uiScale, safeFont * 0.18f) : 0f;
            var cellWidth = badgeSize + countGap + countWidth;
            var interCellGap = Math.Max(10f * uiScale, badgeSize * 0.28f);
            var horizontalStride = cellWidth + interCellGap;
            var verticalStride = Math.Max(badgeSize + 10f * uiScale, safeFont * 1.45f);
            return new MarkerCompactCellGeometry(badgeSize, countWidth, countGap, cellWidth, horizontalStride, verticalStride);
        }

        private static bool IsFinitePositive(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        public static MarkerCompactLayoutSlot[] BuildCompactLayout(int categoryCount)
        {
            var count = Math.Max(0, Math.Min(MaxCategories, categoryCount));
            if (count == 0) return Array.Empty<MarkerCompactLayoutSlot>();
            var rowPattern = CompactRowPattern(count);
            var slots = new MarkerCompactLayoutSlot[count];
            var index = 0;
            var rowCount = rowPattern.Length;
            for (var row = 0; row < rowPattern.Length; row++)
            {
                var rowSize = rowPattern[row];
                var y = (rowCount - 1) * 0.5f - row;
                for (var col = 0; col < rowSize && index < count; col++)
                {
                    var x = col - (rowSize - 1) * 0.5f;
                    slots[index++] = new MarkerCompactLayoutSlot(row, col, rowSize, x, y);
                }
            }
            return slots;
        }

        private static int[] CompactRowPattern(int count)
        {
            switch (count)
            {
                case 1: return new[] { 1 };
                case 2: return new[] { 1, 1 };
                case 3: return new[] { 1, 2 };
                case 4: return new[] { 1, 2, 1 };
                case 5: return new[] { 1, 3, 1 };
                case 6: return new[] { 1, 2, 2, 1 };
                case 7: return new[] { 1, 2, 3, 1 };
                case 8: return new[] { 1, 2, 2, 2, 1 };
                case 9: return new[] { 1, 2, 3, 2, 1 };
                case 10: return new[] { 1, 2, 3, 3, 1 };
                default: return new[] { 1, 3, 3, 3, 1 };
            }
        }

        private static int Compare(MarkerCompactCategoryBadge left, MarkerCompactCategoryBadge right, MarkerCategorySortOrder sortOrder)
        {
            var priority = right.DisplayPriority.CompareTo(left.DisplayPriority);
            if (sortOrder == MarkerCategorySortOrder.LowToHigh) priority = -priority;
            if (priority != 0) return priority;

            var stable = MarkerSemanticCategoryPolicy.StableOrder(left.Category)
                .CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category));
            return sortOrder == MarkerCategorySortOrder.LowToHigh ? -stable : stable;
        }
    }
}
