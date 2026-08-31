using System;

namespace ItemShareFix.Core
{
    public readonly struct MarkerRelativePlacement
    {
        public MarkerRelativePlacement(float offsetX, float offsetY, MarkerHudMode mode, MarkerHudEdge edge, float arrowRotationDegrees,
            bool hudRelocated, bool collisionRelocated, bool messageHudRelocated, int laneSlot, int railSlot)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Mode = mode;
            Edge = edge;
            ArrowRotationDegrees = arrowRotationDegrees;
            HudRelocated = hudRelocated;
            CollisionRelocated = collisionRelocated;
            MessageHudRelocated = messageHudRelocated;
            LaneSlot = laneSlot;
            RailSlot = railSlot;
        }

        public float OffsetX { get; }
        public float OffsetY { get; }
        public MarkerHudMode Mode { get; }
        public MarkerHudEdge Edge { get; }
        public float ArrowRotationDegrees { get; }
        public bool HudRelocated { get; }
        public bool CollisionRelocated { get; }
        public bool MessageHudRelocated { get; }
        public int LaneSlot { get; }
        public int RailSlot { get; }
    }

    public static class MarkerProjectionRelativePlacementPolicy
    {
        public const float FastFollowProjectionJumpPixels1080 = 180f;

        public static MarkerRelativePlacement Capture(MarkerHudProjection projection, MarkerHudPlacement placement)
            => new MarkerRelativePlacement(
                placement.X - projection.X,
                placement.Y - projection.Y,
                placement.Mode,
                placement.Edge,
                placement.ArrowRotationDegrees,
                placement.HudRelocated,
                placement.CollisionRelocated,
                placement.MessageHudRelocated,
                placement.LaneSlot,
                placement.RailSlot);

        public static MarkerHudPlacement Apply(long stableKey, MarkerHudProjection currentProjection, MarkerHudVisualFootprint footprint, MarkerRelativePlacement relative)
        {
            var x = currentProjection.X + relative.OffsetX;
            var y = currentProjection.Y + relative.OffsetY;
            var rect = new MarkerHudRect(x, y, footprint.Width, footprint.Height);
            return new MarkerHudPlacement(
                stableKey,
                currentProjection.Mode,
                currentProjection.Edge,
                x,
                y,
                currentProjection.Mode == MarkerHudMode.OffScreenEdge ? currentProjection.ArrowRotationDegrees : relative.ArrowRotationDegrees,
                relative.LaneSlot,
                relative.RailSlot,
                rect,
                relative.HudRelocated,
                relative.CollisionRelocated,
                relative.MessageHudRelocated);
        }

        public static bool RequiresFastFollow(MarkerHudProjection previous, MarkerHudProjection current, float screenWidth, float screenHeight)
        {
            if (!previous.Valid || !current.Valid) return true;
            if (previous.Mode != current.Mode || previous.Edge != current.Edge) return true;
            var scale = Math.Min(screenWidth / 1920f, screenHeight / 1080f);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) scale = 1f;
            var threshold = FastFollowProjectionJumpPixels1080 * scale;
            var dx = current.X - previous.X;
            var dy = current.Y - previous.Y;
            return dx * dx + dy * dy >= threshold * threshold;
        }
    }
}
