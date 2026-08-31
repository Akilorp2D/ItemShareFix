using System;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Hot-path contracts. The production renderer uses these value objects to decide whether a live TMP
    /// preferred-size result and typography state can be reused without touching TMP again.
    /// </summary>
    public readonly struct MarkerMeasurementCacheKey : IEquatable<MarkerMeasurementCacheKey>
    {
        public MarkerMeasurementCacheKey(
            string semanticText,
            int fontAssetIdentity,
            int materialIdentity,
            int fontSize,
            int screenWidth,
            int screenHeight,
            int typographyRevision)
        {
            SemanticText = semanticText ?? throw new ArgumentNullException(nameof(semanticText));
            FontAssetIdentity = fontAssetIdentity;
            MaterialIdentity = materialIdentity;
            FontSize = fontSize;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            TypographyRevision = typographyRevision;
        }

        public string SemanticText { get; }
        public int FontAssetIdentity { get; }
        public int MaterialIdentity { get; }
        public int FontSize { get; }
        public int ScreenWidth { get; }
        public int ScreenHeight { get; }
        public int TypographyRevision { get; }

        public bool Equals(MarkerMeasurementCacheKey other)
            => string.Equals(SemanticText, other.SemanticText, StringComparison.Ordinal)
               && FontAssetIdentity == other.FontAssetIdentity
               && MaterialIdentity == other.MaterialIdentity
               && FontSize == other.FontSize
               && ScreenWidth == other.ScreenWidth
               && ScreenHeight == other.ScreenHeight
               && TypographyRevision == other.TypographyRevision;

        public override bool Equals(object? obj) => obj is MarkerMeasurementCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SemanticText == null ? 0 : StringComparer.Ordinal.GetHashCode(SemanticText);
                hash = (hash * 397) ^ FontAssetIdentity;
                hash = (hash * 397) ^ MaterialIdentity;
                hash = (hash * 397) ^ FontSize;
                hash = (hash * 397) ^ ScreenWidth;
                hash = (hash * 397) ^ ScreenHeight;
                hash = (hash * 397) ^ TypographyRevision;
                return hash;
            }
        }
    }

    /// <summary>
    /// Meaningful HUD diagnostic state. Preferred/footprint widths are deliberately not part of equality so harmless
    /// TMP metric jitter cannot turn every render-frame callback into an Info log record.
    /// </summary>
    public readonly struct MarkerHudDiagnosticState : IEquatable<MarkerHudDiagnosticState>
    {
        public MarkerHudDiagnosticState(
            MarkerHudMode mode,
            MarkerHudEdge edge,
            int laneSlot,
            int railSlot,
            bool hudRelocated,
            bool messageHudRelocated,
            bool collisionRelocated,
            bool measurementFallback)
        {
            Mode = mode;
            Edge = edge;
            LaneSlot = laneSlot;
            RailSlot = railSlot;
            HudRelocated = hudRelocated;
            MessageHudRelocated = messageHudRelocated;
            CollisionRelocated = collisionRelocated;
            MeasurementFallback = measurementFallback;
        }

        public MarkerHudMode Mode { get; }
        public MarkerHudEdge Edge { get; }
        public int LaneSlot { get; }
        public int RailSlot { get; }
        public bool HudRelocated { get; }
        public bool MessageHudRelocated { get; }
        public bool CollisionRelocated { get; }
        public bool MeasurementFallback { get; }

        public bool Equals(MarkerHudDiagnosticState other)
            => Mode == other.Mode
               && Edge == other.Edge
               && LaneSlot == other.LaneSlot
               && RailSlot == other.RailSlot
               && HudRelocated == other.HudRelocated
               && MessageHudRelocated == other.MessageHudRelocated
               && CollisionRelocated == other.CollisionRelocated
               && MeasurementFallback == other.MeasurementFallback;

        public override bool Equals(object? obj) => obj is MarkerHudDiagnosticState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Mode;
                hash = (hash * 397) ^ (int)Edge;
                hash = (hash * 397) ^ LaneSlot;
                hash = (hash * 397) ^ RailSlot;
                hash = (hash * 397) ^ (HudRelocated ? 1 : 0);
                hash = (hash * 397) ^ (MessageHudRelocated ? 1 : 0);
                hash = (hash * 397) ^ (CollisionRelocated ? 1 : 0);
                hash = (hash * 397) ^ (MeasurementFallback ? 1 : 0);
                return hash;
            }
        }
    }

    public static class MarkerRuntimeHotPathPolicy
    {
        public static bool CanReuseMeasurement(
            bool hasCachedMeasurement,
            MarkerMeasurementCacheKey cachedKey,
            MarkerMeasurementCacheKey requestedKey)
            => hasCachedMeasurement && cachedKey.Equals(requestedKey);

        public static bool ShouldApplyTypography(
            int appliedTypographyRevision,
            int appliedFontSize,
            int requestedTypographyRevision,
            int requestedFontSize)
            => appliedTypographyRevision != requestedTypographyRevision || appliedFontSize != requestedFontSize;

        public static MarkerHudDiagnosticState BuildHudDiagnosticState(
            MarkerHudMode mode,
            MarkerHudEdge edge,
            int laneSlot,
            int railSlot,
            bool hudRelocated,
            bool messageHudRelocated,
            bool collisionRelocated,
            bool measurementFallback,
            float labelPreferredWidth,
            float footprintWidth)
        {
            // Widths are accepted intentionally so the contract is explicit and testable: they remain evidence fields
            // on the emitted record, but do not participate in state equality / Info-log churn.
            _ = labelPreferredWidth;
            _ = footprintWidth;
            return new MarkerHudDiagnosticState(
                mode,
                edge,
                laneSlot,
                railSlot,
                hudRelocated,
                messageHudRelocated,
                collisionRelocated,
                measurementFallback);
        }
    }
}
