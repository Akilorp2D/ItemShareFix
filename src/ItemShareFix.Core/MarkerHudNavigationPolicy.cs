using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemShareFix.Core
{
    public enum MarkerHudMode
    {
        OnScreenWorldAnchor,
        OffScreenEdge,
    }

    public enum MarkerHudEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
    }

    public readonly struct MarkerHudSafeArea
    {
        public MarkerHudSafeArea(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
        public bool Contains(float x, float y) => x >= Left && x <= Right && y >= Bottom && y <= Top;
    }

    public readonly struct MarkerHudExclusionZone
    {
        public MarkerHudExclusionZone(string token, float left, float right, float bottom, float top)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public string Token { get; }
        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
        public bool Contains(float x, float y) => x >= Left && x <= Right && y >= Bottom && y <= Top;
    }

    public readonly struct MarkerHudRect
    {
        public MarkerHudRect(float centerX, float centerY, float width, float height)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = Math.Max(0f, width);
            Height = Math.Max(0f, height);
        }

        public float CenterX { get; }
        public float CenterY { get; }
        public float Width { get; }
        public float Height { get; }
        public float Left => CenterX - Width * 0.5f;
        public float Right => CenterX + Width * 0.5f;
        public float Bottom => CenterY - Height * 0.5f;
        public float Top => CenterY + Height * 0.5f;

        public bool Intersects(MarkerHudRect other)
            => Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;

        public MarkerHudRect Inflated(float amount)
            => new MarkerHudRect(CenterX, CenterY, Width + Math.Max(0f, amount) * 2f, Height + Math.Max(0f, amount) * 2f);
    }

    public readonly struct MarkerHudVisualFootprint
    {
        public MarkerHudVisualFootprint(float width, float height, float indicatorSize, float labelWidth, float paddingX, float gap)
        {
            Width = width;
            Height = height;
            IndicatorSize = indicatorSize;
            LabelWidth = labelWidth;
            PaddingX = paddingX;
            Gap = gap;
        }

        public float Width { get; }
        public float Height { get; }
        public float IndicatorSize { get; }
        public float LabelWidth { get; }
        public float PaddingX { get; }
        public float Gap { get; }
    }

    public readonly struct MarkerHudProjection
    {
        public MarkerHudProjection(
            bool valid,
            MarkerHudMode mode,
            MarkerHudEdge edge,
            float x,
            float y,
            float directionX,
            float directionY,
            float arrowRotationDegrees)
        {
            Valid = valid;
            Mode = mode;
            Edge = edge;
            X = x;
            Y = y;
            DirectionX = directionX;
            DirectionY = directionY;
            ArrowRotationDegrees = arrowRotationDegrees;
        }

        public bool Valid { get; }
        public MarkerHudMode Mode { get; }
        public MarkerHudEdge Edge { get; }
        public float X { get; }
        public float Y { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float ArrowRotationDegrees { get; }
    }

    public readonly struct MarkerHudEdgeCandidate
    {
        public MarkerHudEdgeCandidate(long stableKey, MarkerHudProjection projection)
        {
            StableKey = stableKey;
            Projection = projection;
        }

        public long StableKey { get; }
        public MarkerHudProjection Projection { get; }
    }

    public readonly struct MarkerHudEdgePlacement
    {
        public MarkerHudEdgePlacement(long stableKey, MarkerHudEdge edge, float x, float y, float arrowRotationDegrees, int stackSlot)
        {
            StableKey = stableKey;
            Edge = edge;
            X = x;
            Y = y;
            ArrowRotationDegrees = arrowRotationDegrees;
            StackSlot = stackSlot;
        }

        public long StableKey { get; }
        public MarkerHudEdge Edge { get; }
        public float X { get; }
        public float Y { get; }
        public float ArrowRotationDegrees { get; }
        public int StackSlot { get; }
    }

    public readonly struct MarkerHudPlacementCandidate
    {
        public MarkerHudPlacementCandidate(long stableKey, MarkerHudProjection projection, MarkerHudVisualFootprint footprint)
        {
            StableKey = stableKey;
            Projection = projection;
            Footprint = footprint;
        }

        public long StableKey { get; }
        public MarkerHudProjection Projection { get; }
        public MarkerHudVisualFootprint Footprint { get; }
    }

    public readonly struct MarkerHudPlacement
    {
        public MarkerHudPlacement(
            long stableKey,
            MarkerHudMode mode,
            MarkerHudEdge edge,
            float x,
            float y,
            float arrowRotationDegrees,
            int laneSlot,
            int railSlot,
            MarkerHudRect finalRect,
            bool hudRelocated,
            bool collisionRelocated)
            : this(stableKey, mode, edge, x, y, arrowRotationDegrees, laneSlot, railSlot, finalRect, hudRelocated, collisionRelocated, false)
        {
        }

        public MarkerHudPlacement(
            long stableKey,
            MarkerHudMode mode,
            MarkerHudEdge edge,
            float x,
            float y,
            float arrowRotationDegrees,
            int laneSlot,
            int railSlot,
            MarkerHudRect finalRect,
            bool hudRelocated,
            bool collisionRelocated,
            bool messageHudRelocated)
        {
            StableKey = stableKey;
            Mode = mode;
            Edge = edge;
            X = x;
            Y = y;
            ArrowRotationDegrees = arrowRotationDegrees;
            LaneSlot = laneSlot;
            RailSlot = railSlot;
            FinalRect = finalRect;
            HudRelocated = hudRelocated;
            CollisionRelocated = collisionRelocated;
            MessageHudRelocated = messageHudRelocated;
        }

        public long StableKey { get; }
        public MarkerHudMode Mode { get; }
        public MarkerHudEdge Edge { get; }
        public float X { get; }
        public float Y { get; }
        public float ArrowRotationDegrees { get; }
        public int LaneSlot { get; }
        public int RailSlot { get; }
        public int StackSlot => LaneSlot;
        public MarkerHudRect FinalRect { get; }
        public bool HudRelocated { get; }
        public bool CollisionRelocated { get; }
        public bool MessageHudRelocated { get; }
    }

    /// <summary>
    /// Pure, deterministic HUD projection and final-rectangle placement policy. Coordinates are screen pixels with origin at bottom-left.
    /// Unity camera projection is sampled by the plugin and fed into this policy; no Unity types are required here.
    /// </summary>
    public static class MarkerHudNavigationPolicy
    {
        public const string NativeHudStyleToken = "ISF_ROR2_HUD_NATIVE_V5_C16";
        public const string IndicatorVisualFamilyToken = "ISF_UI_GRAPHIC_INDICATOR_V1";
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float HorizontalSafeInset1080 = 170f;
        public const float BottomSafeInset1080 = 104f;
        public const float TopSafeInset1080 = 88f;
        public const float MinimumEdgeSpacing1080 = 54f;
        public const float EdgeLaneSpacing1080 = 46f;
        public const float MarkerGap1080 = 10f;
        public const float ScreenRectMargin1080 = 12f;
        public const float MeasuredTextSafetyPadding1080 = 12f;
        public const float ConservativeFallbackLabelWidth1080 = 520f;
        public const int MaxDeconflictionLanes = 18;
        public const float MinOnScreenAnchorDisplacement1080 = 110f;
        public const float MaxOnScreenAnchorDisplacement1080 = 260f;
        private const float Epsilon = 0.0001f;

        public static MarkerHudSafeArea GetSafeArea(float screenWidth, float screenHeight)
        {
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f)
                return new MarkerHudSafeArea(0f, 0f, 0f, 0f);

            var scale = ResolutionScale(screenWidth, screenHeight);
            var horizontalInset = Math.Min(screenWidth * 0.22f, HorizontalSafeInset1080 * scale);
            var bottomInset = Math.Min(screenHeight * 0.24f, BottomSafeInset1080 * scale);
            var topInset = Math.Min(screenHeight * 0.22f, TopSafeInset1080 * scale);
            return new MarkerHudSafeArea(horizontalInset, screenWidth - horizontalInset, bottomInset, screenHeight - topInset);
        }

        public static IReadOnlyList<MarkerHudExclusionZone> GetReservedHudZones(float screenWidth, float screenHeight)
        {
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f)
                return Array.Empty<MarkerHudExclusionZone>();

            var scale = ResolutionScale(screenWidth, screenHeight);
            return new[]
            {
                new MarkerHudExclusionZone("health-bottom-left", 0f, Math.Min(screenWidth, 610f * scale), 0f, Math.Min(screenHeight, 260f * scale)),
                new MarkerHudExclusionZone("skills-bottom-right", Math.Max(0f, screenWidth - 710f * scale), screenWidth, 0f, Math.Min(screenHeight, 300f * scale)),
                new MarkerHudExclusionZone("objective-upper-right", Math.Max(0f, screenWidth - 590f * scale), screenWidth, Math.Max(0f, screenHeight - 360f * scale), screenHeight),
                new MarkerHudExclusionZone("items-top-center", Math.Max(0f, screenWidth * 0.5f - 430f * scale), Math.Min(screenWidth, screenWidth * 0.5f + 430f * scale), Math.Max(0f, screenHeight - 160f * scale), screenHeight),
                new MarkerHudExclusionZone("money-player-upper-left", 0f, Math.Min(screenWidth, 500f * scale), Math.Max(0f, screenHeight - 260f * scale), screenHeight),
                // Generic right-side reservation intentionally protects common stats overlays even when their exact runtime rect is unavailable.
                new MarkerHudExclusionZone("stats-overlay-safe-right", Math.Max(0f, screenWidth - 390f * scale), screenWidth, Math.Min(screenHeight, 250f * scale), Math.Max(0f, screenHeight - 250f * scale)),
            };
        }

        /// <summary>
        /// Compatibility helper. Final placement does not rely on this anchor-point approximation; it validates MarkerHudRect instead.
        /// </summary>
        public static bool IsReservedHudPoint(float x, float y, float screenWidth, float screenHeight)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f) return true;
            var scale = ResolutionScale(screenWidth, screenHeight);
            var halfWidth = 145f * scale;
            var halfHeight = 30f * scale;
            var health = x - halfWidth <= Math.Min(screenWidth, 610f * scale) && y - halfHeight <= Math.Min(screenHeight, 260f * scale);
            var skills = x + halfWidth >= Math.Max(0f, screenWidth - 710f * scale) && y - halfHeight <= Math.Min(screenHeight, 300f * scale);
            var objective = x + halfWidth >= Math.Max(0f, screenWidth - 590f * scale) && y + halfHeight >= Math.Max(0f, screenHeight - 360f * scale);
            var itemStripLeft = Math.Max(0f, screenWidth * 0.5f - 430f * scale);
            var itemStripRight = Math.Min(screenWidth, screenWidth * 0.5f + 430f * scale);
            var itemStrip = x + halfWidth >= itemStripLeft
                            && x - halfWidth <= itemStripRight
                            && y + halfHeight >= Math.Max(0f, screenHeight - 160f * scale);
            return health || skills || objective || itemStrip;
        }

        public static MarkerHudVisualFootprint EstimateVisualFootprint(string? label, int roundedDistanceMeters, float screenWidth, float screenHeight)
        {
            var scale = ResolutionScale(screenWidth, screenHeight);
            var fontSize = MarkerPresentationPolicy.BuildNativeHudFontSize((int)Math.Max(1f, screenHeight));
            var text = MarkerPresentationPolicy.BuildNativeHudLabelText(label, roundedDistanceMeters);
            var labelWidth = Clamp(text.Length * fontSize * 0.56f, 72f * scale, 430f * scale);
            var indicatorSize = Math.Max(fontSize + 5f * scale, 28f * scale);
            var paddingX = 10f * scale;
            var gap = 8f * scale;
            var width = paddingX * 2f + indicatorSize + gap + labelWidth;
            var height = Math.Max(indicatorSize, fontSize + 10f * scale) + 8f * scale;
            return new MarkerHudVisualFootprint(width, height, indicatorSize, labelWidth, paddingX, gap);
        }

        /// <summary>
        /// Production footprint builder. preferredLabelWidth/Height come from the live TMP object after native
        /// font/material/fontSize are applied. Invalid measurements fail safe to a conservative supported-label width.
        /// </summary>
        public static MarkerHudVisualFootprint BuildMeasuredVisualFootprint(
            float preferredLabelWidth,
            float preferredLabelHeight,
            float screenWidth,
            float screenHeight)
            => BuildMeasuredVisualFootprint(preferredLabelWidth, preferredLabelHeight, screenWidth, screenHeight, 1f);

        public static MarkerHudVisualFootprint BuildMeasuredVisualFootprint(
            float preferredLabelWidth,
            float preferredLabelHeight,
            float screenWidth,
            float screenHeight,
            float markerScale)
        {
            var resolutionScale = ResolutionScale(screenWidth, screenHeight);
            var uiScale = MarkerClusterPresentationPolicy.ClampMarkerScale(markerScale);
            var scaled = resolutionScale * uiScale;
            var fontSize = MarkerPresentationPolicy.BuildScaledNativeHudFontSize((int)Math.Max(1f, screenHeight), uiScale);
            var measuredWidth = IsFinite(preferredLabelWidth) && preferredLabelWidth > 0f
                ? preferredLabelWidth
                : ConservativeFallbackLabelWidth1080 * scaled;
            var measuredHeight = IsFinite(preferredLabelHeight) && preferredLabelHeight > 0f
                ? preferredLabelHeight
                : fontSize + 10f * scaled;
            var labelWidth = Math.Max(72f * scaled, measuredWidth + MeasuredTextSafetyPadding1080 * scaled);
            var indicatorSize = Math.Max(fontSize + 5f * scaled, 28f * scaled);
            var paddingX = 10f * scaled;
            var gap = 8f * scaled;
            var width = paddingX * 2f + indicatorSize + gap + labelWidth;
            var height = Math.Max(indicatorSize, measuredHeight) + 8f * scaled;
            return new MarkerHudVisualFootprint(width, height, indicatorSize, labelWidth, paddingX, gap);
        }

        public static bool IntersectsDynamicHud(
            MarkerHudRect rect,
            IEnumerable<MarkerHudExclusionZone>? dynamicHudZones,
            float screenWidth,
            float screenHeight)
        {
            if (!IsFiniteRect(rect) || screenWidth <= 0f || screenHeight <= 0f) return true;
            if (dynamicHudZones == null) return false;
            var padding = 4f * ResolutionScale(screenWidth, screenHeight);
            var expanded = rect.Inflated(padding);
            foreach (var zone in dynamicHudZones)
            {
                if (RectIntersectsZone(expanded, zone)) return true;
            }
            return false;
        }

        public static bool IntersectsReservedHud(MarkerHudRect rect, float screenWidth, float screenHeight)
        {
            if (!IsFiniteRect(rect) || screenWidth <= 0f || screenHeight <= 0f) return true;
            var padding = 4f * ResolutionScale(screenWidth, screenHeight);
            var expanded = rect.Inflated(padding);
            foreach (var zone in GetReservedHudZones(screenWidth, screenHeight))
            {
                if (RectIntersectsZone(expanded, zone)) return true;
            }
            return false;
        }

        public static bool IsRectInsideScreen(MarkerHudRect rect, float screenWidth, float screenHeight)
        {
            if (!IsFiniteRect(rect) || !IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f) return false;
            var margin = ScreenRectMargin1080 * ResolutionScale(screenWidth, screenHeight);
            return rect.Left >= margin && rect.Right <= screenWidth - margin && rect.Bottom >= margin && rect.Top <= screenHeight - margin;
        }

        public static MarkerHudProjection ResolveProjection(
            float viewportX,
            float viewportY,
            float viewportDepth,
            float cameraLocalX,
            float cameraLocalY,
            float cameraLocalZ,
            float screenWidth,
            float screenHeight)
        {
            if (!IsFinite(viewportX) || !IsFinite(viewportY) || !IsFinite(viewportDepth)
                || !IsFinite(cameraLocalX) || !IsFinite(cameraLocalY) || !IsFinite(cameraLocalZ)
                || !IsFinite(screenWidth) || !IsFinite(screenHeight)
                || screenWidth <= 0f
                || screenHeight <= 0f)
                return InvalidProjection();

            var safe = GetSafeArea(screenWidth, screenHeight);
            var projectedX = viewportX * screenWidth;
            var projectedY = viewportY * screenHeight;
            if (viewportDepth > Epsilon && safe.Contains(projectedX, projectedY))
            {
                return new MarkerHudProjection(true, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None,
                    projectedX, projectedY, 0f, 0f, 0f);
            }

            if (!TryResolveDirection(viewportX, viewportY, viewportDepth, cameraLocalX, cameraLocalY, cameraLocalZ, out var directionX, out var directionY))
                return InvalidProjection();

            var centerX = screenWidth * 0.5f;
            var centerY = screenHeight * 0.5f;
            var tx = Math.Abs(directionX) < Epsilon
                ? float.PositiveInfinity
                : (directionX > 0f ? safe.Right - centerX : safe.Left - centerX) / directionX;
            var ty = Math.Abs(directionY) < Epsilon
                ? float.PositiveInfinity
                : (directionY > 0f ? safe.Top - centerY : safe.Bottom - centerY) / directionY;

            if (tx <= 0f) tx = float.PositiveInfinity;
            if (ty <= 0f) ty = float.PositiveInfinity;
            var t = Math.Min(tx, ty);
            if (!IsFinite(t) || t <= 0f) return InvalidProjection();

            var edge = tx <= ty
                ? (directionX >= 0f ? MarkerHudEdge.Right : MarkerHudEdge.Left)
                : (directionY >= 0f ? MarkerHudEdge.Top : MarkerHudEdge.Bottom);
            var x = Clamp(centerX + directionX * t, safe.Left, safe.Right);
            var y = Clamp(centerY + directionY * t, safe.Bottom, safe.Top);
            ResolveReservedEdgePoint(edge, x, y, screenWidth, screenHeight, out x, out y);

            var rotation = -(float)(Math.Atan2(directionX, directionY) * 180.0 / Math.PI);
            return new MarkerHudProjection(true, MarkerHudMode.OffScreenEdge, edge, x, y, directionX, directionY, rotation);
        }

        public static IReadOnlyList<MarkerHudPlacement> ResolvePlacements(
            IEnumerable<MarkerHudPlacementCandidate> candidates,
            float screenWidth,
            float screenHeight)
            => ResolvePlacements(candidates, screenWidth, screenHeight, Array.Empty<MarkerHudExclusionZone>());

        public static IReadOnlyList<MarkerHudPlacement> ResolvePlacements(
            IEnumerable<MarkerHudPlacementCandidate> candidates,
            float screenWidth,
            float screenHeight,
            IEnumerable<MarkerHudExclusionZone>? dynamicHudZones)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f)
                return Array.Empty<MarkerHudPlacement>();

            var dynamicZones = dynamicHudZones?.ToArray() ?? Array.Empty<MarkerHudExclusionZone>();
            var ordered = candidates
                .Where(c => c.Projection.Valid && IsValidFootprint(c.Footprint))
                .OrderBy(c => c.Projection.Mode == MarkerHudMode.OffScreenEdge ? 0 : 1)
                .ThenBy(c => c.Projection.Mode == MarkerHudMode.OffScreenEdge ? (int)c.Projection.Edge : 0)
                .ThenBy(c => c.Projection.Mode == MarkerHudMode.OffScreenEdge
                    ? Axis(c.Projection.Edge, c.Projection.X, c.Projection.Y)
                    : c.Projection.Y)
                .ThenBy(c => c.Projection.Mode == MarkerHudMode.OffScreenEdge ? 0f : c.Projection.X)
                .ThenBy(c => c.StableKey)
                .ToArray();

            var occupied = new List<MarkerHudRect>(ordered.Length);
            var result = new List<MarkerHudPlacement>(ordered.Length);
            foreach (var candidate in ordered)
            {
                var placement = candidate.Projection.Mode == MarkerHudMode.OffScreenEdge
                    ? ResolveEdgePlacement(candidate, occupied, dynamicZones, screenWidth, screenHeight)
                    : ResolveOnScreenPlacement(candidate, occupied, dynamicZones, screenWidth, screenHeight);
                occupied.Add(placement.FinalRect);
                result.Add(placement);
            }
            return result;
        }

        /// <summary>
        /// Production single-marker fast path. It preserves the exact static/dynamic HUD and edge-navigation
        /// placement rules without paying sorting, multi-marker occupied-buffer, or LINQ costs.
        /// </summary>
        public static MarkerHudPlacement ResolveSinglePlacement(
            MarkerHudPlacementCandidate candidate,
            float screenWidth,
            float screenHeight,
            IReadOnlyList<MarkerHudExclusionZone>? dynamicHudZones = null)
        {
            if (!candidate.Projection.Valid || !IsValidFootprint(candidate.Footprint))
                throw new ArgumentException("Candidate projection/footprint must be valid.", nameof(candidate));
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(screenWidth));

            var zones = dynamicHudZones ?? Array.Empty<MarkerHudExclusionZone>();
            var occupied = Array.Empty<MarkerHudRect>();
            return candidate.Projection.Mode == MarkerHudMode.OffScreenEdge
                ? ResolveEdgePlacement(candidate, occupied, zones, screenWidth, screenHeight)
                : ResolveOnScreenPlacement(candidate, occupied, zones, screenWidth, screenHeight);
        }

        /// <summary>
        /// Allocation-bounded production multi-marker solver. Caller-owned buffers are cleared and reused; no
        /// candidate ToArray/OrderBy/result-list allocation occurs on the active marker frame path.
        /// </summary>
        public static void ResolvePlacementsBuffered(
            IReadOnlyList<MarkerHudPlacementCandidate> candidates,
            float screenWidth,
            float screenHeight,
            IReadOnlyList<MarkerHudExclusionZone>? dynamicHudZones,
            List<MarkerHudPlacementCandidate> orderedBuffer,
            List<MarkerHudRect> occupiedBuffer,
            List<MarkerHudPlacement> resultBuffer)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (orderedBuffer == null) throw new ArgumentNullException(nameof(orderedBuffer));
            if (occupiedBuffer == null) throw new ArgumentNullException(nameof(occupiedBuffer));
            if (resultBuffer == null) throw new ArgumentNullException(nameof(resultBuffer));

            orderedBuffer.Clear();
            occupiedBuffer.Clear();
            resultBuffer.Clear();
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f) return;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Projection.Valid && IsValidFootprint(candidate.Footprint)) orderedBuffer.Add(candidate);
            }
            orderedBuffer.Sort(MarkerPlacementCandidateComparer.Instance);

            var zones = dynamicHudZones ?? Array.Empty<MarkerHudExclusionZone>();
            for (var i = 0; i < orderedBuffer.Count; i++)
            {
                var candidate = orderedBuffer[i];
                var placement = candidate.Projection.Mode == MarkerHudMode.OffScreenEdge
                    ? ResolveEdgePlacement(candidate, occupiedBuffer, zones, screenWidth, screenHeight)
                    : ResolveOnScreenPlacement(candidate, occupiedBuffer, zones, screenWidth, screenHeight);
                occupiedBuffer.Add(placement.FinalRect);
                resultBuffer.Add(placement);
            }
        }

        private sealed class MarkerPlacementCandidateComparer : IComparer<MarkerHudPlacementCandidate>
        {
            public static readonly MarkerPlacementCandidateComparer Instance = new MarkerPlacementCandidateComparer();

            public int Compare(MarkerHudPlacementCandidate left, MarkerHudPlacementCandidate right)
            {
                var leftEdge = left.Projection.Mode == MarkerHudMode.OffScreenEdge;
                var rightEdge = right.Projection.Mode == MarkerHudMode.OffScreenEdge;
                if (leftEdge != rightEdge) return leftEdge ? -1 : 1;
                if (leftEdge)
                {
                    var edgeCompare = ((int)left.Projection.Edge).CompareTo((int)right.Projection.Edge);
                    if (edgeCompare != 0) return edgeCompare;
                    var axisCompare = Axis(left.Projection.Edge, left.Projection.X, left.Projection.Y)
                        .CompareTo(Axis(right.Projection.Edge, right.Projection.X, right.Projection.Y));
                    if (axisCompare != 0) return axisCompare;
                }
                else
                {
                    var yCompare = left.Projection.Y.CompareTo(right.Projection.Y);
                    if (yCompare != 0) return yCompare;
                    var xCompare = left.Projection.X.CompareTo(right.Projection.X);
                    if (xCompare != 0) return xCompare;
                }
                return left.StableKey.CompareTo(right.StableKey);
            }
        }

        /// <summary>
        /// Production multi-marker solver. Stable-key rank memory is applied before placement so
        /// sub-hysteresis projection noise cannot exchange collision slots. Caller-owned buffers remain reusable.
        /// </summary>
        public static void ResolvePlacementsBufferedStable(
            IReadOnlyList<MarkerHudPlacementCandidate> candidates,
            float screenWidth,
            float screenHeight,
            IReadOnlyList<MarkerHudExclusionZone>? dynamicHudZones,
            IReadOnlyDictionary<long, int>? previousRanks,
            List<MarkerHudPlacementCandidate> orderedBuffer,
            List<MarkerHudRect> occupiedBuffer,
            List<MarkerHudPlacement> resultBuffer)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (orderedBuffer == null) throw new ArgumentNullException(nameof(orderedBuffer));
            if (occupiedBuffer == null) throw new ArgumentNullException(nameof(occupiedBuffer));
            if (resultBuffer == null) throw new ArgumentNullException(nameof(resultBuffer));

            occupiedBuffer.Clear();
            resultBuffer.Clear();
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f)
            {
                orderedBuffer.Clear();
                return;
            }

            MarkerPlacementStabilityPolicy.BuildStableOrderBuffered(candidates, previousRanks, screenWidth, screenHeight, orderedBuffer);
            var zones = dynamicHudZones ?? Array.Empty<MarkerHudExclusionZone>();
            for (var i = 0; i < orderedBuffer.Count; i++)
            {
                var candidate = orderedBuffer[i];
                if (!candidate.Projection.Valid || !IsValidFootprint(candidate.Footprint)) continue;
                var placement = candidate.Projection.Mode == MarkerHudMode.OffScreenEdge
                    ? ResolveEdgePlacement(candidate, occupiedBuffer, zones, screenWidth, screenHeight)
                    : ResolveOnScreenPlacement(candidate, occupiedBuffer, zones, screenWidth, screenHeight);
                occupiedBuffer.Add(placement.FinalRect);
                resultBuffer.Add(placement);
            }
        }

        public static float GetMaxOnScreenAnchorDisplacement(MarkerHudVisualFootprint footprint, float screenWidth, float screenHeight)
        {
            var scale = ResolutionScale(screenWidth, screenHeight);
            var footprintDriven = Math.Max(footprint.Height * 2.6f, footprint.Width * 0.62f);
            return Clamp(footprintDriven, MinOnScreenAnchorDisplacement1080 * scale, MaxOnScreenAnchorDisplacement1080 * scale);
        }

        public static float OnScreenAnchorDisplacement(MarkerHudProjection projection, MarkerHudPlacement placement)
        {
            var dx = placement.X - projection.X;
            var dy = placement.Y - projection.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Compatibility API. Production uses ResolvePlacements with per-marker footprints.
        /// </summary>
        public static IReadOnlyList<MarkerHudEdgePlacement> Deconflict(
            IEnumerable<MarkerHudEdgeCandidate> candidates,
            float screenWidth,
            float screenHeight)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var scale = ResolutionScale(screenWidth, screenHeight);
            var compatibilityFootprint = new MarkerHudVisualFootprint(290f * scale, 60f * scale, 30f * scale, 230f * scale, 10f * scale, 8f * scale);
            return ResolvePlacements(
                    candidates.Select(c => new MarkerHudPlacementCandidate(c.StableKey, c.Projection, compatibilityFootprint)),
                    screenWidth,
                    screenHeight)
                .Where(x => x.Mode == MarkerHudMode.OffScreenEdge)
                .Select(x => new MarkerHudEdgePlacement(x.StableKey, x.Edge, x.X, x.Y, x.ArrowRotationDegrees, x.LaneSlot))
                .ToArray();
        }

        public static string StyleTokenForMode(MarkerHudMode mode)
        {
            _ = mode;
            return NativeHudStyleToken;
        }

        private static MarkerHudPlacement ResolveEdgePlacement(
            MarkerHudPlacementCandidate candidate,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight)
        {
            var projection = candidate.Projection;
            var initialRect = new MarkerHudRect(projection.X, projection.Y, candidate.Footprint.Width, candidate.Footprint.Height);
            var initialStaticHud = IntersectsReservedHud(initialRect, screenWidth, screenHeight);
            var initialMessageHud = IntersectsDynamicHud(initialRect, dynamicHudZones, screenWidth, screenHeight);
            var initialHud = initialStaticHud || initialMessageHud;
            var initialCollision = IntersectsOccupied(initialRect, occupied, screenWidth, screenHeight);
            var scale = ResolutionScale(screenWidth, screenHeight);
            var safe = GetSafeArea(screenWidth, screenHeight);
            var gap = MarkerGap1080 * scale;
            var axisStep = Math.Max(MinimumEdgeSpacing1080 * scale,
                (projection.Edge == MarkerHudEdge.Left || projection.Edge == MarkerHudEdge.Right
                    ? candidate.Footprint.Height
                    : candidate.Footprint.Width) + gap);
            var crossDimension = projection.Edge == MarkerHudEdge.Left || projection.Edge == MarkerHudEdge.Right
                ? candidate.Footprint.Width
                : candidate.Footprint.Height;
            var laneSpacing = Math.Max(EdgeLaneSpacing1080 * scale, crossDimension * 0.72f + gap);
            var desiredAxis = Axis(projection.Edge, projection.X, projection.Y);

            for (var lane = 0; lane < MaxDeconflictionLanes; lane++)
            {
                for (var trial = 0; trial <= 80; trial++)
                {
                    var railSlot = TrialOffset(trial);
                    var axis = desiredAxis + railSlot * axisStep;
                    if (!TryBuildVisualRailCenter(projection.Edge, axis, lane, laneSpacing, safe, candidate.Footprint, screenWidth, screenHeight, out var x, out var y)) continue;
                    var rect = new MarkerHudRect(x, y, candidate.Footprint.Width, candidate.Footprint.Height);
                    if (!IsPlacementFree(rect, occupied, dynamicHudZones, screenWidth, screenHeight)) continue;
                    var moved = !NearlyEqual(x, projection.X) || !NearlyEqual(y, projection.Y);
                    return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OffScreenEdge, projection.Edge,
                        x, y, projection.ArrowRotationDegrees, lane, railSlot, rect,
                        hudRelocated: initialHud && moved,
                        collisionRelocated: initialCollision && moved,
                        messageHudRelocated: initialMessageHud && moved);
                }
            }

            if (TryFindAnyFreePlacement(candidate.Footprint, projection.X, projection.Y, occupied, dynamicHudZones, screenWidth, screenHeight, out var fallbackX, out var fallbackY))
            {
                var rect = new MarkerHudRect(fallbackX, fallbackY, candidate.Footprint.Width, candidate.Footprint.Height);
                return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OffScreenEdge, projection.Edge,
                    fallbackX, fallbackY, projection.ArrowRotationDegrees, MaxDeconflictionLanes, 0, rect,
                    hudRelocated: initialHud,
                    collisionRelocated: initialCollision || !NearlyEqual(fallbackX, projection.X) || !NearlyEqual(fallbackY, projection.Y),
                    messageHudRelocated: initialMessageHud);
            }

            // Physical screen area can be exhausted with pathological marker counts. Preserve every identity rather than silently dropping one.
            var forcedX = Clamp(projection.X, candidate.Footprint.Width * 0.5f, screenWidth - candidate.Footprint.Width * 0.5f);
            var forcedY = Clamp(projection.Y, candidate.Footprint.Height * 0.5f, screenHeight - candidate.Footprint.Height * 0.5f);
            var forcedRect = new MarkerHudRect(forcedX, forcedY, candidate.Footprint.Width, candidate.Footprint.Height);
            return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OffScreenEdge, projection.Edge,
                forcedX, forcedY, projection.ArrowRotationDegrees, MaxDeconflictionLanes + 1, 0, forcedRect,
                hudRelocated: initialHud,
                collisionRelocated: initialCollision,
                messageHudRelocated: initialMessageHud);
        }

        private static MarkerHudPlacement ResolveOnScreenPlacement(
            MarkerHudPlacementCandidate candidate,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight)
        {
            var projection = candidate.Projection;
            var initialRect = new MarkerHudRect(projection.X, projection.Y, candidate.Footprint.Width, candidate.Footprint.Height);
            var initialStaticHud = IntersectsReservedHud(initialRect, screenWidth, screenHeight);
            var initialMessageHud = IntersectsDynamicHud(initialRect, dynamicHudZones, screenWidth, screenHeight);
            var initialHud = initialStaticHud || initialMessageHud;
            var initialCollision = IntersectsOccupied(initialRect, occupied, screenWidth, screenHeight);
            if (IsPlacementFree(initialRect, occupied, dynamicHudZones, screenWidth, screenHeight))
            {
                return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None,
                    projection.X, projection.Y, 0f, 0, 0, initialRect, false, false);
            }

            var scale = ResolutionScale(screenWidth, screenHeight);
            var stepX = Math.Max(42f * scale, Math.Min(112f * scale, candidate.Footprint.Width * 0.28f));
            var stepY = Math.Max(40f * scale, candidate.Footprint.Height + MarkerGap1080 * scale);
            var maxDisplacement = GetMaxOnScreenAnchorDisplacement(candidate.Footprint, screenWidth, screenHeight);
            var maxDisplacementSquared = maxDisplacement * maxDisplacement;
            var maxRings = Math.Max(1, (int)Math.Ceiling(maxDisplacement / Math.Max(1f, Math.Min(stepX, stepY))));
            for (var ring = 1; ring <= maxRings; ring++)
            {
                var offsetX = stepX * ring;
                var offsetY = stepY * ring;
                for (var trial = 0; trial < 8; trial++)
                {
                    float dx;
                    float dy;
                    switch (trial)
                    {
                        case 0: dx = 0f; dy = -offsetY; break;
                        case 1: dx = offsetX; dy = 0f; break;
                        case 2: dx = -offsetX; dy = 0f; break;
                        case 3: dx = 0f; dy = offsetY; break;
                        case 4: dx = offsetX; dy = -offsetY; break;
                        case 5: dx = -offsetX; dy = -offsetY; break;
                        case 6: dx = offsetX; dy = offsetY; break;
                        default: dx = -offsetX; dy = offsetY; break;
                    }
                    if (dx * dx + dy * dy > maxDisplacementSquared + Epsilon) continue;
                    var x = projection.X + dx;
                    var y = projection.Y + dy;
                    var rect = new MarkerHudRect(x, y, candidate.Footprint.Width, candidate.Footprint.Height);
                    if (!IsPlacementFree(rect, occupied, dynamicHudZones, screenWidth, screenHeight)) continue;
                    return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None,
                        x, y, 0f, ring, 0, rect,
                        hudRelocated: initialHud,
                        collisionRelocated: initialCollision,
                        messageHudRelocated: initialMessageHud);
                }
            }

            // Ordinary marker-vs-marker deconfliction remains bounded, but HUD zones
            // are hard exclusions. If the world anchor itself is inside static/dynamic HUD and no bounded candidate
            // can escape it, deterministically choose the nearest free hard-HUD escape even when that requires
            // exceeding the normal anchor-displacement cap.
            if (initialHud && TryFindNearestHardHudEscape(
                    candidate.Footprint, projection.X, projection.Y, occupied, dynamicHudZones, screenWidth, screenHeight,
                    out var hudEscapeX, out var hudEscapeY))
            {
                var hudEscapeRect = new MarkerHudRect(hudEscapeX, hudEscapeY, candidate.Footprint.Width, candidate.Footprint.Height);
                return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None,
                    hudEscapeX, hudEscapeY, 0f, maxRings + 1, 0, hudEscapeRect,
                    hudRelocated: true,
                    collisionRelocated: initialCollision,
                    messageHudRelocated: initialMessageHud);
            }

            // On-screen labels intentionally do not free-float across the viewport for ordinary
            // marker collisions. If the bounded neighborhood is saturated (or the hard-HUD escape is genuinely
            // exhausted), density aggregation remains the presentation declutter mechanism and this representative
            // stays anchored as close as the screen rectangle permits.
            var forcedX = Clamp(projection.X, candidate.Footprint.Width * 0.5f, screenWidth - candidate.Footprint.Width * 0.5f);
            var forcedY = Clamp(projection.Y, candidate.Footprint.Height * 0.5f, screenHeight - candidate.Footprint.Height * 0.5f);
            var forcedDx = forcedX - projection.X;
            var forcedDy = forcedY - projection.Y;
            var forcedDistance = (float)Math.Sqrt(forcedDx * forcedDx + forcedDy * forcedDy);
            if (forcedDistance > maxDisplacement && forcedDistance > Epsilon)
            {
                var ratio = maxDisplacement / forcedDistance;
                forcedX = projection.X + forcedDx * ratio;
                forcedY = projection.Y + forcedDy * ratio;
            }
            var forcedRect = new MarkerHudRect(forcedX, forcedY, candidate.Footprint.Width, candidate.Footprint.Height);
            return new MarkerHudPlacement(candidate.StableKey, MarkerHudMode.OnScreenWorldAnchor, MarkerHudEdge.None,
                forcedX, forcedY, 0f, maxRings + 1, 0, forcedRect,
                hudRelocated: initialHud,
                collisionRelocated: initialCollision,
                messageHudRelocated: initialMessageHud);
        }

        private static bool TryFindNearestHardHudEscape(
            MarkerHudVisualFootprint footprint,
            float desiredX,
            float desiredY,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight,
            out float resolvedX,
            out float resolvedY)
        {
            resolvedX = 0f;
            resolvedY = 0f;
            if (!IsValidFootprint(footprint) || screenWidth <= 0f || screenHeight <= 0f) return false;

            var scale = ResolutionScale(screenWidth, screenHeight);
            var screenMargin = ScreenRectMargin1080 * scale;
            var hudPadding = 4f * scale;
            var collisionGap = MarkerGap1080 * scale;
            var halfWidth = footprint.Width * 0.5f;
            var halfHeight = footprint.Height * 0.5f;
            var minX = halfWidth + screenMargin;
            var maxX = screenWidth - halfWidth - screenMargin;
            var minY = halfHeight + screenMargin;
            var maxY = screenHeight - halfHeight - screenMargin;
            if (minX > maxX || minY > maxY) return false;

            var anchorX = Clamp(desiredX, minX, maxX);
            var anchorY = Clamp(desiredY, minY, maxY);
            var found = false;
            var bestDistanceSquared = float.PositiveInfinity;
            var bestX = 0f;
            var bestY = 0f;

            ConsiderHardHudEscapeCandidate(anchorX, anchorY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(minX, anchorY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(maxX, anchorY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(anchorX, minY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(anchorX, maxY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);

            foreach (var zone in GetReservedHudZones(screenWidth, screenHeight))
            {
                ConsiderHardHudEscapeZone(zone, hudPadding, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                    screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            }

            for (var i = 0; i < dynamicHudZones.Count; i++)
            {
                ConsiderHardHudEscapeZone(dynamicHudZones[i], hudPadding, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                    screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            }

            // Occupied markers are not themselves hard HUD, but a valid HUD escape must also avoid them. Their
            // boundaries therefore become deterministic secondary candidate surfaces when they block the nearest
            // HUD-side solution.
            for (var i = 0; i < occupied.Count; i++)
            {
                var rect = occupied[i];
                var zone = new MarkerHudExclusionZone(
                    "occupied",
                    rect.Left - collisionGap * 0.5f,
                    rect.Right + collisionGap * 0.5f,
                    rect.Bottom - collisionGap * 0.5f,
                    rect.Top + collisionGap * 0.5f);
                ConsiderHardHudEscapeZone(zone, collisionGap * 0.5f, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                    screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            }

            // Boundary candidates solve the common case exactly. A deterministic anchor-centred ring scan closes
            // pathological combinations of overlapping hard zones/occupied markers without restoring the generic
            // on-screen viewport free-float path. The scan is entered only after bounded placement already failed.
            var searchStep = Math.Max(6f, 8f * scale);
            var maxSearchRadius = Math.Max(
                Math.Max(Math.Abs(desiredX - minX), Math.Abs(maxX - desiredX)),
                Math.Max(Math.Abs(desiredY - minY), Math.Abs(maxY - desiredY)));
            var maxSearchRing = Math.Max(1, (int)Math.Ceiling(maxSearchRadius / searchStep));
            for (var ring = 1; ring <= maxSearchRing; ring++)
            {
                var radius = ring * searchStep;
                if (found && radius * radius > bestDistanceSquared + Epsilon) break;

                for (var ix = -ring; ix <= ring; ix++)
                {
                    var x = desiredX + ix * searchStep;
                    ConsiderHardHudEscapeCandidate(x, desiredY - radius, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                        screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
                    ConsiderHardHudEscapeCandidate(x, desiredY + radius, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                        screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
                }

                for (var iy = -ring + 1; iy <= ring - 1; iy++)
                {
                    var y = desiredY + iy * searchStep;
                    ConsiderHardHudEscapeCandidate(desiredX - radius, y, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                        screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
                    ConsiderHardHudEscapeCandidate(desiredX + radius, y, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                        screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
                }
            }

            if (!found) return false;
            resolvedX = bestX;
            resolvedY = bestY;
            return true;
        }

        private static void ConsiderHardHudEscapeZone(
            MarkerHudExclusionZone zone,
            float padding,
            float desiredX,
            float desiredY,
            MarkerHudVisualFootprint footprint,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight,
            float minX,
            float maxX,
            float minY,
            float maxY,
            ref bool found,
            ref float bestDistanceSquared,
            ref float bestX,
            ref float bestY)
        {
            var halfWidth = footprint.Width * 0.5f;
            var halfHeight = footprint.Height * 0.5f;
            var leftX = zone.Left - halfWidth - padding;
            var rightX = zone.Right + halfWidth + padding;
            var bottomY = zone.Bottom - halfHeight - padding;
            var topY = zone.Top + halfHeight + padding;
            var preferredX = Clamp(desiredX, minX, maxX);
            var preferredY = Clamp(desiredY, minY, maxY);

            ConsiderHardHudEscapeCandidate(leftX, preferredY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(rightX, preferredY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(preferredX, bottomY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(preferredX, topY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);

            ConsiderHardHudEscapeCandidate(leftX, bottomY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(leftX, topY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(rightX, bottomY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
            ConsiderHardHudEscapeCandidate(rightX, topY, desiredX, desiredY, footprint, occupied, dynamicHudZones,
                screenWidth, screenHeight, minX, maxX, minY, maxY, ref found, ref bestDistanceSquared, ref bestX, ref bestY);
        }

        private static void ConsiderHardHudEscapeCandidate(
            float x,
            float y,
            float desiredX,
            float desiredY,
            MarkerHudVisualFootprint footprint,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight,
            float minX,
            float maxX,
            float minY,
            float maxY,
            ref bool found,
            ref float bestDistanceSquared,
            ref float bestX,
            ref float bestY)
        {
            if (!IsFinite(x) || !IsFinite(y) || x < minX || x > maxX || y < minY || y > maxY) return;
            var rect = new MarkerHudRect(x, y, footprint.Width, footprint.Height);
            if (!IsPlacementFree(rect, occupied, dynamicHudZones, screenWidth, screenHeight)) return;

            var dx = x - desiredX;
            var dy = y - desiredY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > bestDistanceSquared + Epsilon) return;
            if (Math.Abs(distanceSquared - bestDistanceSquared) <= Epsilon
                && (y > bestY + Epsilon || (Math.Abs(y - bestY) <= Epsilon && x >= bestX))) return;

            found = true;
            bestDistanceSquared = distanceSquared;
            bestX = x;
            bestY = y;
        }

        private static bool TryFindAnyFreePlacement(
            MarkerHudVisualFootprint footprint,
            float desiredX,
            float desiredY,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight,
            out float resolvedX,
            out float resolvedY)
        {
            resolvedX = 0f;
            resolvedY = 0f;
            var scale = ResolutionScale(screenWidth, screenHeight);
            var margin = ScreenRectMargin1080 * scale;
            var minX = footprint.Width * 0.5f + margin;
            var maxX = screenWidth - footprint.Width * 0.5f - margin;
            var minY = footprint.Height * 0.5f + margin;
            var maxY = screenHeight - footprint.Height * 0.5f - margin;
            if (minX > maxX || minY > maxY) return false;

            var xStep = Math.Max(footprint.Width * 0.55f, 80f * scale);
            var yStep = Math.Max(footprint.Height + MarkerGap1080 * scale, 48f * scale);
            var found = false;
            var bestDistanceSquared = float.PositiveInfinity;
            var bestX = 0f;
            var bestY = 0f;
            for (var y = minY; y <= maxY + 0.01f; y += yStep)
            {
                for (var x = minX; x <= maxX + 0.01f; x += xStep)
                {
                    var rect = new MarkerHudRect(x, y, footprint.Width, footprint.Height);
                    if (!IsPlacementFree(rect, occupied, dynamicHudZones, screenWidth, screenHeight)) continue;
                    var dx = x - desiredX;
                    var dy = y - desiredY;
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > bestDistanceSquared + 0.0001f) continue;
                    if (Math.Abs(distanceSquared - bestDistanceSquared) <= 0.0001f
                        && (y > bestY + 0.0001f || (Math.Abs(y - bestY) <= 0.0001f && x >= bestX))) continue;
                    found = true;
                    bestDistanceSquared = distanceSquared;
                    bestX = x;
                    bestY = y;
                }
            }
            if (!found) return false;
            resolvedX = bestX;
            resolvedY = bestY;
            return true;
        }

        private static bool IsPlacementFree(
            MarkerHudRect rect,
            IReadOnlyList<MarkerHudRect> occupied,
            IReadOnlyList<MarkerHudExclusionZone> dynamicHudZones,
            float screenWidth,
            float screenHeight)
            => IsRectInsideScreen(rect, screenWidth, screenHeight)
               && !IntersectsReservedHud(rect, screenWidth, screenHeight)
               && !IntersectsDynamicHud(rect, dynamicHudZones, screenWidth, screenHeight)
               && !IntersectsOccupied(rect, occupied, screenWidth, screenHeight);

        private static bool IntersectsOccupied(MarkerHudRect rect, IReadOnlyList<MarkerHudRect> occupied, float screenWidth, float screenHeight)
        {
            var gap = MarkerGap1080 * ResolutionScale(screenWidth, screenHeight);
            var expanded = rect.Inflated(gap * 0.5f);
            for (var i = 0; i < occupied.Count; i++)
            {
                if (expanded.Intersects(occupied[i].Inflated(gap * 0.5f))) return true;
            }
            return false;
        }

        private static bool TryResolveDirection(
            float viewportX,
            float viewportY,
            float viewportDepth,
            float localX,
            float localY,
            float localZ,
            out float directionX,
            out float directionY)
        {
            directionX = 0f;
            directionY = 0f;

            if (viewportDepth > Epsilon)
            {
                directionX = viewportX - 0.5f;
                directionY = viewportY - 0.5f;
            }
            else
            {
                var horizontal = (float)Math.Sqrt(localX * localX + localZ * localZ);
                var length = (float)Math.Sqrt(localX * localX + localY * localY + localZ * localZ);
                if (length <= Epsilon) return false;

                directionX = horizontal > Epsilon ? localX / horizontal : 0f;
                var vertical = localY / length;
                var rear = horizontal > Epsilon ? Math.Max(0f, -localZ / horizontal) : 1f;
                directionY = vertical - rear * 0.82f;
                if (Math.Abs(directionX) + Math.Abs(directionY) < 0.08f) directionY = -1f;
            }

            var magnitude = (float)Math.Sqrt(directionX * directionX + directionY * directionY);
            if (!IsFinite(magnitude) || magnitude <= Epsilon) return false;
            directionX /= magnitude;
            directionY /= magnitude;
            return true;
        }

        private static void ResolveReservedEdgePoint(
            MarkerHudEdge edge,
            float x,
            float y,
            float screenWidth,
            float screenHeight,
            out float resolvedX,
            out float resolvedY)
        {
            resolvedX = x;
            resolvedY = y;
            if (!IsReservedHudPoint(x, y, screenWidth, screenHeight)) return;

            var safe = GetSafeArea(screenWidth, screenHeight);
            var scale = ResolutionScale(screenWidth, screenHeight);
            var step = Math.Max(12f, 18f * scale);
            var desiredAxis = Axis(edge, x, y);
            for (var trial = 1; trial <= 160; trial++)
            {
                var offset = TrialOffset(trial) * step;
                var axis = desiredAxis + offset;
                if (!TryBuildRailPoint(edge, axis, 0, 0f, safe, out var testX, out var testY)) continue;
                if (IsReservedHudPoint(testX, testY, screenWidth, screenHeight)) continue;
                resolvedX = testX;
                resolvedY = testY;
                return;
            }
        }

        private static bool TryBuildRailPoint(
            MarkerHudEdge edge,
            float axis,
            int lane,
            float laneSpacing,
            MarkerHudSafeArea safe,
            out float x,
            out float y)
        {
            x = 0f;
            y = 0f;
            switch (edge)
            {
                case MarkerHudEdge.Left:
                    if (axis < safe.Bottom || axis > safe.Top) return false;
                    x = safe.Left + lane * laneSpacing;
                    y = axis;
                    return x <= safe.Right;
                case MarkerHudEdge.Right:
                    if (axis < safe.Bottom || axis > safe.Top) return false;
                    x = safe.Right - lane * laneSpacing;
                    y = axis;
                    return x >= safe.Left;
                case MarkerHudEdge.Top:
                    if (axis < safe.Left || axis > safe.Right) return false;
                    x = axis;
                    y = safe.Top - lane * laneSpacing;
                    return y >= safe.Bottom;
                case MarkerHudEdge.Bottom:
                    if (axis < safe.Left || axis > safe.Right) return false;
                    x = axis;
                    y = safe.Bottom + lane * laneSpacing;
                    return y <= safe.Top;
                default:
                    return false;
            }
        }

        private static bool TryBuildVisualRailCenter(
            MarkerHudEdge edge,
            float axis,
            int lane,
            float laneSpacing,
            MarkerHudSafeArea safe,
            MarkerHudVisualFootprint footprint,
            float screenWidth,
            float screenHeight,
            out float x,
            out float y)
        {
            x = 0f;
            y = 0f;
            var scale = ResolutionScale(screenWidth, screenHeight);
            var margin = ScreenRectMargin1080 * scale;
            var halfWidth = footprint.Width * 0.5f;
            var halfHeight = footprint.Height * 0.5f;
            var minX = Math.Max(safe.Left, halfWidth + margin);
            var maxX = Math.Min(safe.Right, screenWidth - halfWidth - margin);
            var minY = Math.Max(safe.Bottom, halfHeight + margin);
            var maxY = Math.Min(safe.Top, screenHeight - halfHeight - margin);
            if (minX > maxX || minY > maxY) return false;

            switch (edge)
            {
                case MarkerHudEdge.Left:
                    if (axis < minY || axis > maxY) return false;
                    x = minX + lane * laneSpacing;
                    y = axis;
                    return x <= maxX;
                case MarkerHudEdge.Right:
                    if (axis < minY || axis > maxY) return false;
                    x = maxX - lane * laneSpacing;
                    y = axis;
                    return x >= minX;
                case MarkerHudEdge.Top:
                    if (axis < minX || axis > maxX) return false;
                    x = axis;
                    y = maxY - lane * laneSpacing;
                    return y >= minY;
                case MarkerHudEdge.Bottom:
                    if (axis < minX || axis > maxX) return false;
                    x = axis;
                    y = minY + lane * laneSpacing;
                    return y <= maxY;
                default:
                    return false;
            }
        }

        private static bool RectIntersectsZone(MarkerHudRect rect, MarkerHudExclusionZone zone)
            => rect.Left < zone.Right && rect.Right > zone.Left && rect.Bottom < zone.Top && rect.Top > zone.Bottom;

        private static bool IsValidFootprint(MarkerHudVisualFootprint footprint)
            => IsFinite(footprint.Width) && IsFinite(footprint.Height) && footprint.Width > 0f && footprint.Height > 0f;

        private static bool IsFiniteRect(MarkerHudRect rect)
            => IsFinite(rect.CenterX) && IsFinite(rect.CenterY) && IsFinite(rect.Width) && IsFinite(rect.Height);

        private static float Axis(MarkerHudEdge edge, float x, float y)
            => edge == MarkerHudEdge.Left || edge == MarkerHudEdge.Right ? y : x;

        private static int TrialOffset(int trial)
        {
            if (trial <= 0) return 0;
            var magnitude = (trial + 1) / 2;
            return trial % 2 == 1 ? magnitude : -magnitude;
        }

        private static float ResolutionScale(float screenWidth, float screenHeight)
        {
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f) return 1f;
            var scale = Math.Min(screenWidth / ReferenceWidth, screenHeight / ReferenceHeight);
            return Clamp(scale, 0.60f, 2.0f);
        }

        private static MarkerHudProjection InvalidProjection()
            => new MarkerHudProjection(false, MarkerHudMode.OffScreenEdge, MarkerHudEdge.None, 0f, 0f, 0f, 0f, 0f);

        private static bool NearlyEqual(float a, float b) => Math.Abs(a - b) <= 0.01f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
