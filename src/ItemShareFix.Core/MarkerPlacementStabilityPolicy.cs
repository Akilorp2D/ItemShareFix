using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Deterministic placement-memory policy. Previous stable-key ranks seed the next dense solve;
    /// adjacent candidates exchange ownership only after a resolution-scaled meaningful crossing. Caller-owned buffers
    /// keep the active frame path allocation-bounded.
    /// </summary>
    public static class MarkerPlacementStabilityPolicy
    {
        public const float RankHysteresisPixels1080 = 14f;
        public const float RelocationResponsePerSecond = 13f;
        public const float MaxInterpolationDeltaSeconds = 0.10f;

        public static void BuildStableOrderBuffered(
            IReadOnlyList<MarkerHudPlacementCandidate> candidates,
            IReadOnlyDictionary<long, int>? previousRanks,
            float screenWidth,
            float screenHeight,
            List<MarkerHudPlacementCandidate> output)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.Projection.Valid) output.Add(candidate);
            }

            // Deterministic raw order for first appearance and for candidates that have no remembered rank.
            InsertionSortRaw(output);
            if (previousRanks == null || previousRanks.Count == 0 || output.Count < 2) return;

            // Seed retained identities by their last solved rank inside the same mode/edge rail.
            for (var i = 1; i < output.Count; i++)
            {
                var current = output[i];
                var j = i - 1;
                while (j >= 0 && ShouldMoveBeforeByMemory(current, output[j], previousRanks))
                {
                    output[j + 1] = output[j];
                    j--;
                }
                output[j + 1] = current;
            }

            // Bubble only meaningful crossings. Tiny projection-order noise cannot flap two retained slots.
            var hysteresis = RankHysteresisPixels1080 * ResolutionScale(screenWidth, screenHeight);
            for (var pass = 0; pass < output.Count; pass++)
            {
                var changed = false;
                for (var i = 0; i + 1 < output.Count; i++)
                {
                    var left = output[i];
                    var right = output[i + 1];
                    if (!SameRail(left, right) || !MeaningfullyCrossed(left, right, hysteresis)) continue;
                    output[i] = right;
                    output[i + 1] = left;
                    changed = true;
                }
                if (!changed) break;
            }
        }

        public static float SmoothCoordinate(float current, float target, float unscaledDeltaTime)
        {
            if (!IsFinite(current) || !IsFinite(target)) return target;
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime <= 0f) return current;
            var dt = Math.Min(MaxInterpolationDeltaSeconds, unscaledDeltaTime);
            var alpha = 1f - (float)Math.Exp(-RelocationResponsePerSecond * dt);
            return current + (target - current) * alpha;
        }

        public static void ClampDisplacementFromAnchor(
            float anchorX,
            float anchorY,
            float candidateX,
            float candidateY,
            float maxDisplacement,
            out float clampedX,
            out float clampedY)
        {
            clampedX = candidateX;
            clampedY = candidateY;
            if (!IsFinite(anchorX) || !IsFinite(anchorY) || !IsFinite(candidateX) || !IsFinite(candidateY)) return;
            var bound = Math.Max(0f, maxDisplacement);
            var dx = candidateX - anchorX;
            var dy = candidateY - anchorY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared <= bound * bound || distanceSquared <= 0f) return;
            var scale = bound / (float)Math.Sqrt(distanceSquared);
            clampedX = anchorX + dx * scale;
            clampedY = anchorY + dy * scale;
        }

        public static bool IsMeaningfulAxisCrossing(float previousLeaderAxis, float previousFollowerAxis, float currentLeaderAxis, float currentFollowerAxis, float hysteresisPixels)
        {
            var wasLeaderFirst = previousLeaderAxis <= previousFollowerAxis;
            if (!wasLeaderFirst) return currentLeaderAxis + Math.Max(0f, hysteresisPixels) < currentFollowerAxis;
            return currentLeaderAxis > currentFollowerAxis + Math.Max(0f, hysteresisPixels);
        }

        private static void InsertionSortRaw(List<MarkerHudPlacementCandidate> output)
        {
            for (var i = 1; i < output.Count; i++)
            {
                var current = output[i];
                var j = i - 1;
                while (j >= 0 && CompareRaw(current, output[j]) < 0)
                {
                    output[j + 1] = output[j];
                    j--;
                }
                output[j + 1] = current;
            }
        }

        private static bool ShouldMoveBeforeByMemory(
            MarkerHudPlacementCandidate candidate,
            MarkerHudPlacementCandidate existing,
            IReadOnlyDictionary<long, int> previousRanks)
        {
            var groupCompare = CompareRail(candidate, existing);
            if (groupCompare != 0) return groupCompare < 0;
            var candidateKnown = previousRanks.TryGetValue(candidate.StableKey, out var candidateRank);
            var existingKnown = previousRanks.TryGetValue(existing.StableKey, out var existingRank);
            if (candidateKnown && existingKnown) return candidateRank < existingRank;
            if (candidateKnown != existingKnown) return candidateKnown;
            return CompareRaw(candidate, existing) < 0;
        }

        private static bool MeaningfullyCrossed(MarkerHudPlacementCandidate left, MarkerHudPlacementCandidate right, float hysteresis)
        {
            var leftAxis = SortAxis(left);
            var rightAxis = SortAxis(right);
            return leftAxis > rightAxis + hysteresis;
        }

        private static float SortAxis(MarkerHudPlacementCandidate candidate)
            => candidate.Projection.Mode == MarkerHudMode.OffScreenEdge
                ? (candidate.Projection.Edge == MarkerHudEdge.Left || candidate.Projection.Edge == MarkerHudEdge.Right
                    ? candidate.Projection.Y
                    : candidate.Projection.X)
                : candidate.Projection.Y;

        private static int CompareRaw(MarkerHudPlacementCandidate left, MarkerHudPlacementCandidate right)
        {
            var groupCompare = CompareRail(left, right);
            if (groupCompare != 0) return groupCompare;
            var axisCompare = SortAxis(left).CompareTo(SortAxis(right));
            if (axisCompare != 0) return axisCompare;
            if (left.Projection.Mode == MarkerHudMode.OnScreenWorldAnchor)
            {
                var xCompare = left.Projection.X.CompareTo(right.Projection.X);
                if (xCompare != 0) return xCompare;
            }
            return left.StableKey.CompareTo(right.StableKey);
        }

        private static int CompareRail(MarkerHudPlacementCandidate left, MarkerHudPlacementCandidate right)
        {
            var leftEdge = left.Projection.Mode == MarkerHudMode.OffScreenEdge;
            var rightEdge = right.Projection.Mode == MarkerHudMode.OffScreenEdge;
            if (leftEdge != rightEdge) return leftEdge ? -1 : 1;
            if (!leftEdge) return 0;
            return ((int)left.Projection.Edge).CompareTo((int)right.Projection.Edge);
        }

        private static bool SameRail(MarkerHudPlacementCandidate left, MarkerHudPlacementCandidate right)
            => CompareRail(left, right) == 0;

        private static float ResolutionScale(float screenWidth, float screenHeight)
        {
            if (!IsFinite(screenWidth) || !IsFinite(screenHeight) || screenWidth <= 0f || screenHeight <= 0f) return 1f;
            return Math.Max(0.55f, Math.Min(screenWidth / MarkerHudNavigationPolicy.ReferenceWidth, screenHeight / MarkerHudNavigationPolicy.ReferenceHeight));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
