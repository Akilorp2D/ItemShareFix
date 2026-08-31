using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public readonly struct MarkerDensityCandidate
    {
        public MarkerDensityCandidate(
            long stableKey,
            PersonalMarkerKind markerKind,
            MarkerHudMode mode,
            MarkerHudEdge edge,
            float x,
            float y,
            int distanceMeters)
        {
            StableKey = stableKey;
            MarkerKind = markerKind;
            Mode = mode;
            Edge = edge;
            X = x;
            Y = y;
            DistanceMeters = Math.Max(0, distanceMeters);
        }

        public long StableKey { get; }
        public PersonalMarkerKind MarkerKind { get; }
        public MarkerHudMode Mode { get; }
        public MarkerHudEdge Edge { get; }
        public float X { get; }
        public float Y { get; }
        public int DistanceMeters { get; }
    }

    public readonly struct MarkerDensityDecision
    {
        public MarkerDensityDecision(long stableKey, int hiddenMemberCount, int clusterSize, bool compactCluster)
        {
            StableKey = stableKey;
            HiddenMemberCount = Math.Max(0, hiddenMemberCount);
            ClusterSize = Math.Max(1, clusterSize);
            CompactCluster = compactCluster;
        }

        public long StableKey { get; }
        public int HiddenMemberCount { get; }
        public int ClusterSize { get; }
        public bool CompactCluster { get; }
    }

    public readonly struct MarkerDensityCluster
    {
        public MarkerDensityCluster(MarkerDensityCandidate representative, int memberCount)
        {
            Representative = representative;
            MemberCount = Math.Max(1, memberCount);
        }

        public MarkerDensityCandidate Representative { get; }
        public int MemberCount { get; }

        public MarkerDensityCluster AddMember()
            => new MarkerDensityCluster(Representative, MemberCount + 1);
    }

    /// <summary>
    /// Presentation-only density policy. It never removes logical pending identities; it reduces only
    /// the number of full-label representatives sent to the UI renderer. Grouping is deterministic under input-order
    /// permutation and bounded by MaxLogicalMarkers with fixed-pass O(N^2) work and caller-owned reusable buffers.
    /// </summary>
    public static class MarkerDensityPolicy
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float BaseClusterWidth1080 = 230f;
        public const float BaseClusterHeight1080 = 92f;
        public const float EdgeClusterAxis1080 = 108f;
        public const float DensityCoverageFraction = 0.075f;
        public const float NominalFullLabelWidth1080 = 270f;
        public const float NominalFullLabelHeight1080 = 46f;
        public const int MinimumRepresentativeBudget = 8;
        public const int MaximumRepresentativeBudget = 24;
        public const int MaximumGroupingPasses = 5;
        public const float GroupingExpansionPerPass = 1.70f;

        public static int CalculateRepresentativeBudget(float screenWidth, float screenHeight)
        {
            if (!IsFinitePositive(screenWidth) || !IsFinitePositive(screenHeight)) return MinimumRepresentativeBudget;
            var scale = ResolutionScale(screenWidth, screenHeight);
            var nominalArea = NominalFullLabelWidth1080 * NominalFullLabelHeight1080 * scale * scale;
            var budget = nominalArea <= 0f
                ? MinimumRepresentativeBudget
                : (int)Math.Floor(screenWidth * screenHeight * DensityCoverageFraction / nominalArea);
            if (budget < MinimumRepresentativeBudget) return MinimumRepresentativeBudget;
            return budget > MaximumRepresentativeBudget ? MaximumRepresentativeBudget : budget;
        }

        public static void ResolveBuffered(
            IReadOnlyList<MarkerDensityCandidate> candidates,
            float screenWidth,
            float screenHeight,
            List<MarkerDensityCandidate> orderedBuffer,
            List<MarkerDensityCluster> clusterBuffer,
            List<MarkerDensityDecision> resultBuffer)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (orderedBuffer == null) throw new ArgumentNullException(nameof(orderedBuffer));
            if (clusterBuffer == null) throw new ArgumentNullException(nameof(clusterBuffer));
            if (resultBuffer == null) throw new ArgumentNullException(nameof(resultBuffer));

            orderedBuffer.Clear();
            clusterBuffer.Clear();
            resultBuffer.Clear();
            if (!IsFinitePositive(screenWidth) || !IsFinitePositive(screenHeight)) return;

            var count = Math.Min(candidates.Count, MarkerPresentationPolicy.MaxLogicalMarkers);
            for (var i = 0; i < count; i++) orderedBuffer.Add(candidates[i]);
            orderedBuffer.Sort(MarkerDensityCandidateComparer.Instance);
            if (orderedBuffer.Count == 0) return;

            var budget = CalculateRepresentativeBudget(screenWidth, screenHeight);
            var expansion = 1f;
            for (var pass = 0; pass < MaximumGroupingPasses; pass++)
            {
                BuildClusters(orderedBuffer, screenWidth, screenHeight, expansion, clusterBuffer);
                if (clusterBuffer.Count <= budget) break;
                expansion *= GroupingExpansionPerPass;
            }

            // If extremely sparse positions still exceed the screen-area budget, merge overflow groups into the nearest
            // retained representative. This changes only presentation count; every hidden identity remains in registry/state.
            if (clusterBuffer.Count > budget)
            {
                for (var i = clusterBuffer.Count - 1; i >= budget; i--)
                {
                    var overflow = clusterBuffer[i];
                    var nearest = FindNearestCompatibleCluster(clusterBuffer, budget, overflow.Representative);
                    if (nearest < 0) nearest = FindNearestCluster(clusterBuffer, budget, overflow.Representative);
                    if (nearest >= 0)
                    {
                        var target = clusterBuffer[nearest];
                        clusterBuffer[nearest] = new MarkerDensityCluster(target.Representative, target.MemberCount + overflow.MemberCount);
                    }
                    clusterBuffer.RemoveAt(i);
                }
            }

            for (var i = 0; i < clusterBuffer.Count; i++)
            {
                var cluster = clusterBuffer[i];
                resultBuffer.Add(new MarkerDensityDecision(
                    cluster.Representative.StableKey,
                    cluster.MemberCount - 1,
                    cluster.MemberCount,
                    compactCluster: cluster.MemberCount > 1));
            }
        }

        public static int SumRepresentedIdentities(IReadOnlyList<MarkerDensityDecision> decisions)
        {
            if (decisions == null) throw new ArgumentNullException(nameof(decisions));
            var total = 0;
            for (var i = 0; i < decisions.Count; i++) total += decisions[i].ClusterSize;
            return total;
        }

        private static void BuildClusters(
            IReadOnlyList<MarkerDensityCandidate> ordered,
            float screenWidth,
            float screenHeight,
            float expansion,
            List<MarkerDensityCluster> clusters)
        {
            clusters.Clear();
            var scale = ResolutionScale(screenWidth, screenHeight);
            var width = BaseClusterWidth1080 * scale * expansion;
            var height = BaseClusterHeight1080 * scale * expansion;
            var edgeAxis = EdgeClusterAxis1080 * scale * expansion;

            for (var i = 0; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                var nearest = -1;
                var nearestDistance = float.PositiveInfinity;
                for (var c = 0; c < clusters.Count; c++)
                {
                    var representative = clusters[c].Representative;
                    if (!SamePresentationRail(representative, candidate)) continue;
                    var dx = Math.Abs(candidate.X - representative.X);
                    var dy = Math.Abs(candidate.Y - representative.Y);
                    var compatible = candidate.Mode == MarkerHudMode.OffScreenEdge
                        ? AxisDistance(candidate.Edge, dx, dy) <= edgeAxis
                        : dx <= width && dy <= height;
                    if (!compatible) continue;
                    var distance = dx * dx + dy * dy;
                    if (distance >= nearestDistance) continue;
                    nearest = c;
                    nearestDistance = distance;
                }

                if (nearest < 0) clusters.Add(new MarkerDensityCluster(candidate, 1));
                else clusters[nearest] = clusters[nearest].AddMember();
            }
        }

        private static int FindNearestCompatibleCluster(IReadOnlyList<MarkerDensityCluster> clusters, int limit, MarkerDensityCandidate candidate)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < limit; i++)
            {
                var representative = clusters[i].Representative;
                if (!SamePresentationRail(representative, candidate)) continue;
                var dx = representative.X - candidate.X;
                var dy = representative.Y - candidate.Y;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private static int FindNearestCluster(IReadOnlyList<MarkerDensityCluster> clusters, int limit, MarkerDensityCandidate candidate)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < limit; i++)
            {
                var representative = clusters[i].Representative;
                var dx = representative.X - candidate.X;
                var dy = representative.Y - candidate.Y;
                var distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private static bool SamePresentationRail(MarkerDensityCandidate left, MarkerDensityCandidate right)
            => left.Mode == right.Mode
               && (left.Mode != MarkerHudMode.OffScreenEdge || left.Edge == right.Edge);

        private static float AxisDistance(MarkerHudEdge edge, float dx, float dy)
            => edge == MarkerHudEdge.Left || edge == MarkerHudEdge.Right ? dy : dx;

        private static float ResolutionScale(float screenWidth, float screenHeight)
            => Math.Max(0.55f, Math.Min(screenWidth / ReferenceWidth, screenHeight / ReferenceHeight));

        private static bool IsFinitePositive(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private sealed class MarkerDensityCandidateComparer : IComparer<MarkerDensityCandidate>
        {
            public static readonly MarkerDensityCandidateComparer Instance = new MarkerDensityCandidateComparer();

            public int Compare(MarkerDensityCandidate left, MarkerDensityCandidate right)
            {
                // Command/item-choice targets remain the most actionable presentation identities, then nearer markers.
                var kindCompare = KindPriority(left.MarkerKind).CompareTo(KindPriority(right.MarkerKind));
                if (kindCompare != 0) return kindCompare;
                var distanceCompare = left.DistanceMeters.CompareTo(right.DistanceMeters);
                if (distanceCompare != 0) return distanceCompare;
                return left.StableKey.CompareTo(right.StableKey);
            }

            private static int KindPriority(PersonalMarkerKind kind)
                => kind == PersonalMarkerKind.CommandPicker ? 0 : 1;
        }
    }
}
