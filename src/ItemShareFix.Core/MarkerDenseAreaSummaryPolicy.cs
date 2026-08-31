using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemShareFix.Core
{
    public sealed class MarkerDenseAreaPresentationNode
    {
        internal MarkerDenseAreaPresentationNode(
            long stableKey,
            bool isDenseSummary,
            long[] physicalClusterKeys,
            MarkerSemanticCluster presentationCluster)
        {
            StableKey = stableKey;
            IsDenseSummary = isDenseSummary;
            PhysicalClusterKeys = physicalClusterKeys ?? Array.Empty<long>();
            PresentationCluster = presentationCluster ?? throw new ArgumentNullException(nameof(presentationCluster));
        }

        public long StableKey { get; }
        public bool IsDenseSummary { get; }
        public IReadOnlyList<long> PhysicalClusterKeys { get; }
        public MarkerSemanticCluster PresentationCluster { get; }
        public MarkerWorldPoint WorldAnchor => PresentationCluster.WorldAnchor;
        public IReadOnlyList<long> MemberStableKeys => PresentationCluster.MemberStableKeys;
        public int TotalCount => PresentationCluster.TotalCount;
        public string MemberFingerprint => PresentationCluster.MemberFingerprint;
    }

    public sealed class MarkerDenseAreaUpdate
    {
        internal MarkerDenseAreaUpdate(IReadOnlyList<MarkerDenseAreaPresentationNode> nodes, bool membershipChanged)
        {
            Nodes = nodes;
            MembershipChanged = membershipChanged;
        }

        public IReadOnlyList<MarkerDenseAreaPresentationNode> Nodes { get; }
        public bool MembershipChanged { get; }
    }

    /// <summary>
    /// World-space summary layer above the physical-cluster tracker. Inputs are physical cluster anchors,
    /// composition and member identities only. Camera, FOV, viewport and screen coordinates are intentionally absent.
    /// </summary>
    public sealed class MarkerDenseAreaSummaryTracker
    {
        // Calibration: physical piles whose anchors are <=12 m apart may collapse into one user-perceived dense area.
        // Once linked, 16 m split hysteresis plus 0.35 s dwell prevents completion/motion churn from exploding a card.
        public const float MergeRadiusMeters = 12.0f;
        public const float SplitRadiusMeters = 16.0f;
        public const double ThresholdTransitionDwellSeconds = 0.35d;
        public const int MinimumPhysicalClustersForSummary = 2;

        private readonly struct PairKey : IEquatable<PairKey>
        {
            public PairKey(long a, long b)
            {
                if (a <= b) { A = a; B = b; }
                else { A = b; B = a; }
            }
            public long A { get; }
            public long B { get; }
            public bool Equals(PairKey other) => A == other.A && B == other.B;
            public override bool Equals(object? obj) => obj is PairKey other && Equals(other);
            public override int GetHashCode() => (A.GetHashCode() * 397) ^ B.GetHashCode();
        }

        private sealed class PairState
        {
            public bool Linked;
            public bool Pending;
            public bool PendingTarget;
            public double PendingSince;
        }

        private sealed class PreviousDenseOwner
        {
            public PreviousDenseOwner(long ownerKey, long[] memberKeys)
            {
                OwnerKey = ownerKey;
                MemberKeys = memberKeys;
            }
            public long OwnerKey { get; }
            public long[] MemberKeys { get; }
        }

        private readonly List<MarkerSemanticCluster> _orderedClusters = new List<MarkerSemanticCluster>();
        private readonly Dictionary<PairKey, PairState> _pairStates = new Dictionary<PairKey, PairState>();
        private readonly List<PairKey> _stalePairs = new List<PairKey>();
        private readonly List<MarkerDenseAreaPresentationNode> _nodes = new List<MarkerDenseAreaPresentationNode>();
        private readonly List<PreviousDenseOwner> _previousDenseOwners = new List<PreviousDenseOwner>();
        private string _lastMembershipSignature = string.Empty;

        public IReadOnlyList<MarkerDenseAreaPresentationNode> Nodes => _nodes;

        public MarkerDenseAreaUpdate Update(IReadOnlyList<MarkerSemanticCluster> physicalClusters, double nowSeconds)
        {
            if (physicalClusters == null) throw new ArgumentNullException(nameof(physicalClusters));
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) nowSeconds = 0d;

            _orderedClusters.Clear();
            for (var i = 0; i < physicalClusters.Count; i++)
            {
                if (physicalClusters[i] != null) _orderedClusters.Add(physicalClusters[i]);
            }
            _orderedClusters.Sort((left, right) => left.StableKey.CompareTo(right.StableKey));

            ReconcileNewPairsImmediately();
            AdvanceExistingPairs(nowSeconds);
            PrunePairStates();

            var nextNodes = BuildPresentationNodes();
            var signature = BuildMembershipSignature(nextNodes);
            var changed = !string.Equals(signature, _lastMembershipSignature, StringComparison.Ordinal);
            _lastMembershipSignature = signature;

            _nodes.Clear();
            _nodes.AddRange(nextNodes);
            CapturePreviousDenseOwners();
            return new MarkerDenseAreaUpdate(_nodes, changed);
        }

        public void Clear()
        {
            _orderedClusters.Clear();
            _pairStates.Clear();
            _stalePairs.Clear();
            _nodes.Clear();
            _previousDenseOwners.Clear();
            _lastMembershipSignature = string.Empty;
        }

        private void ReconcileNewPairsImmediately()
        {
            var mergeSquared = MergeRadiusMeters * MergeRadiusMeters;
            for (var i = 0; i < _orderedClusters.Count; i++)
            {
                for (var j = i + 1; j < _orderedClusters.Count; j++)
                {
                    var key = new PairKey(_orderedClusters[i].StableKey, _orderedClusters[j].StableKey);
                    if (_pairStates.ContainsKey(key)) continue;
                    _pairStates[key] = new PairState
                    {
                        Linked = MarkerWorldPoint.DistanceSquared(_orderedClusters[i].WorldAnchor, _orderedClusters[j].WorldAnchor) <= mergeSquared,
                    };
                }
            }
        }

        private void AdvanceExistingPairs(double nowSeconds)
        {
            var mergeSquared = MergeRadiusMeters * MergeRadiusMeters;
            var splitSquared = SplitRadiusMeters * SplitRadiusMeters;
            foreach (var pair in _pairStates)
            {
                var left = FindCluster(pair.Key.A);
                var right = FindCluster(pair.Key.B);
                if (left == null || right == null) continue;
                var distanceSquared = MarkerWorldPoint.DistanceSquared(left.WorldAnchor, right.WorldAnchor);
                var state = pair.Value;
                if (state.Linked)
                {
                    if (distanceSquared <= splitSquared)
                    {
                        state.Pending = false;
                        continue;
                    }
                    AdvancePending(state, false, nowSeconds);
                }
                else
                {
                    if (distanceSquared >= mergeSquared)
                    {
                        state.Pending = false;
                        continue;
                    }
                    AdvancePending(state, true, nowSeconds);
                }
            }
        }

        private static void AdvancePending(PairState state, bool target, double nowSeconds)
        {
            if (!state.Pending || state.PendingTarget != target)
            {
                state.Pending = true;
                state.PendingTarget = target;
                state.PendingSince = nowSeconds;
                return;
            }
            if (nowSeconds - state.PendingSince < ThresholdTransitionDwellSeconds) return;
            state.Linked = target;
            state.Pending = false;
        }

        private void PrunePairStates()
        {
            _stalePairs.Clear();
            foreach (var pair in _pairStates.Keys)
            {
                if (FindCluster(pair.A) == null || FindCluster(pair.B) == null) _stalePairs.Add(pair);
            }
            for (var i = 0; i < _stalePairs.Count; i++) _pairStates.Remove(_stalePairs[i]);
        }

        private MarkerSemanticCluster? FindCluster(long key)
        {
            var low = 0;
            var high = _orderedClusters.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var candidate = _orderedClusters[mid].StableKey;
                if (candidate == key) return _orderedClusters[mid];
                if (candidate < key) low = mid + 1;
                else high = mid - 1;
            }
            return null;
        }

        private List<MarkerDenseAreaPresentationNode> BuildPresentationNodes()
        {
            var result = new List<MarkerDenseAreaPresentationNode>(_orderedClusters.Count);
            if (_orderedClusters.Count == 0) return result;

            var parent = new int[_orderedClusters.Count];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            foreach (var pair in _pairStates)
            {
                if (!pair.Value.Linked) continue;
                var left = IndexOfCluster(pair.Key.A);
                var right = IndexOfCluster(pair.Key.B);
                if (left >= 0 && right >= 0) Union(parent, left, right);
            }

            var groups = new Dictionary<int, List<MarkerSemanticCluster>>();
            for (var i = 0; i < _orderedClusters.Count; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var group))
                {
                    group = new List<MarkerSemanticCluster>();
                    groups.Add(root, group);
                }
                group.Add(_orderedClusters[i]);
            }

            var claimedPreviousOwners = new HashSet<long>();
            foreach (var group in groups.Values)
            {
                group.Sort((left, right) => left.StableKey.CompareTo(right.StableKey));
                var memberKeys = CollectMemberKeys(group);
                if (group.Count < MinimumPhysicalClustersForSummary)
                {
                    // A summary that already owns surviving local-pending members stays the presentation owner even
                    // when collection temporarily reduces the lower PhysicalWorldCluster count to one. This prevents
                    // the user-visible summary from exploding into a singleton card after pickup completion. A lone
                    // physical cluster that was never summarized still remains an ordinary physical presentation node.
                    if (TryClaimPreviousOwnerKey(memberKeys, claimedPreviousOwners, out var survivingOwnerKey))
                    {
                        var survivingSummary = BuildSummaryCluster(survivingOwnerKey, group, memberKeys);
                        result.Add(new MarkerDenseAreaPresentationNode(
                            survivingOwnerKey,
                            true,
                            new[] { group[0].StableKey },
                            survivingSummary));
                    }
                    else
                    {
                        var cluster = group[0];
                        result.Add(new MarkerDenseAreaPresentationNode(cluster.StableKey, false, new[] { cluster.StableKey }, cluster));
                    }
                    continue;
                }

                var ownerKey = ResolvePersistentOwnerKey(memberKeys, claimedPreviousOwners);
                var presentationCluster = BuildSummaryCluster(ownerKey, group, memberKeys);
                var physicalKeys = new long[group.Count];
                for (var i = 0; i < group.Count; i++) physicalKeys[i] = group[i].StableKey;
                result.Add(new MarkerDenseAreaPresentationNode(ownerKey, true, physicalKeys, presentationCluster));
            }

            result.Sort((left, right) => left.StableKey.CompareTo(right.StableKey));
            return result;
        }

        private long ResolvePersistentOwnerKey(long[] memberKeys, HashSet<long> claimedPreviousOwners)
        {
            if (TryClaimPreviousOwnerKey(memberKeys, claimedPreviousOwners, out var previousOwnerKey))
                return previousOwnerKey;

            var physicalHash = MarkerWorldClusterTracker.StableClusterKey(memberKeys);
            var newOwner = -physicalHash;
            if (newOwner == 0 || newOwner == long.MinValue) newOwner = -1;
            claimedPreviousOwners.Add(newOwner);
            return newOwner;
        }


        private bool TryClaimPreviousOwnerKey(long[] memberKeys, HashSet<long> claimedPreviousOwners, out long ownerKey)
        {
            var bestOwner = 0L;
            var bestOverlap = 0;
            for (var i = 0; i < _previousDenseOwners.Count; i++)
            {
                var previous = _previousDenseOwners[i];
                if (claimedPreviousOwners.Contains(previous.OwnerKey)) continue;
                var overlap = CountOverlap(previous.MemberKeys, memberKeys);
                if (overlap > bestOverlap || (overlap == bestOverlap && overlap > 0 && previous.OwnerKey < bestOwner))
                {
                    bestOverlap = overlap;
                    bestOwner = previous.OwnerKey;
                }
            }
            if (bestOverlap > 0)
            {
                claimedPreviousOwners.Add(bestOwner);
                ownerKey = bestOwner;
                return true;
            }

            ownerKey = 0L;
            return false;
        }

        private static MarkerSemanticCluster BuildSummaryCluster(long ownerKey, IReadOnlyList<MarkerSemanticCluster> clusters, long[] memberKeys)
        {
            var compositionMap = new Dictionary<MarkerSemanticCategory, int>();
            var itemMap = new Dictionary<MarkerItemLifetimeKey, MutableAggregate>();
            double x = 0d, y = 0d, z = 0d;
            var totalWeight = 0;
            var temporaryPhysicalMemberCount = 0;
            var mixedLifetimeMemberCount = 0;
            var unknownLifetimeMemberCount = 0;

            for (var i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                var weight = Math.Max(1, cluster.TotalCount);
                x += cluster.WorldAnchor.X * weight;
                y += cluster.WorldAnchor.Y * weight;
                z += cluster.WorldAnchor.Z * weight;
                totalWeight += weight;
                temporaryPhysicalMemberCount += cluster.TemporaryPhysicalMemberCount;
                mixedLifetimeMemberCount += cluster.MixedLifetimeMemberCount;
                unknownLifetimeMemberCount += cluster.UnknownLifetimeMemberCount;

                for (var c = 0; c < cluster.Composition.Count; c++)
                {
                    var entry = cluster.Composition[c];
                    compositionMap.TryGetValue(entry.Category, out var count);
                    compositionMap[entry.Category] = count + entry.Count;
                }
                for (var r = 0; r < cluster.ItemRows.Count; r++)
                {
                    var row = cluster.ItemRows[r];
                    var aggregateKey = new MarkerItemLifetimeKey(row.ItemIdentity, row.Lifetime);
                    if (!itemMap.TryGetValue(aggregateKey, out var aggregate))
                    {
                        aggregate = new MutableAggregate(row.ItemIdentity, row.LocalizedName, row.Category, row.Lifetime);
                        itemMap.Add(aggregateKey, aggregate);
                    }
                    aggregate.Count += row.Count;
                }
            }

            var composition = new List<MarkerCategoryCount>(compositionMap.Count);
            foreach (var pair in compositionMap) composition.Add(new MarkerCategoryCount(pair.Key, pair.Value));
            composition.Sort((left, right) => MarkerSemanticCategoryPolicy.StableOrder(left.Category).CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category)));

            var rows = new List<MarkerItemAggregate>(itemMap.Count);
            foreach (var aggregate in itemMap.Values)
                rows.Add(new MarkerItemAggregate(aggregate.ItemIdentity, aggregate.LocalizedName, aggregate.Category, aggregate.Count, aggregate.Lifetime));
            rows.Sort((left, right) =>
            {
                var category = MarkerSemanticCategoryPolicy.StableOrder(left.Category).CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category));
                if (category != 0) return category;
                var name = string.Compare(left.LocalizedName, right.LocalizedName, StringComparison.Ordinal);
                if (name != 0) return name;
                var identity = string.Compare(left.ItemIdentity, right.ItemIdentity, StringComparison.Ordinal);
                if (identity != 0) return identity;
                return MarkerLifetimePolicy.DetailedSortRank(left.Lifetime).CompareTo(MarkerLifetimePolicy.DetailedSortRank(right.Lifetime));
            });

            var anchor = totalWeight > 0
                ? new MarkerWorldPoint((float)(x / totalWeight), (float)(y / totalWeight), (float)(z / totalWeight))
                : default;
            return new MarkerSemanticCluster(
                ownerKey,
                "DENSE:" + MarkerWorldClusterTracker.MemberFingerprint(memberKeys),
                anchor,
                memberKeys,
                composition.ToArray(),
                rows.ToArray(),
                temporaryPhysicalMemberCount,
                mixedLifetimeMemberCount,
                unknownLifetimeMemberCount);
        }

        private static long[] CollectMemberKeys(IReadOnlyList<MarkerSemanticCluster> clusters)
        {
            var keys = new List<long>();
            for (var i = 0; i < clusters.Count; i++)
                for (var m = 0; m < clusters[i].MemberStableKeys.Count; m++)
                    keys.Add(clusters[i].MemberStableKeys[m]);
            keys.Sort();
            return keys.ToArray();
        }

        private void CapturePreviousDenseOwners()
        {
            _previousDenseOwners.Clear();
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!node.IsDenseSummary) continue;
                var keys = new long[node.MemberStableKeys.Count];
                for (var m = 0; m < keys.Length; m++) keys[m] = node.MemberStableKeys[m];
                _previousDenseOwners.Add(new PreviousDenseOwner(node.StableKey, keys));
            }
        }

        private static int CountOverlap(IReadOnlyList<long> left, IReadOnlyList<long> right)
        {
            var i = 0;
            var j = 0;
            var count = 0;
            while (i < left.Count && j < right.Count)
            {
                if (left[i] == right[j]) { count++; i++; j++; }
                else if (left[i] < right[j]) i++;
                else j++;
            }
            return count;
        }

        private static string BuildMembershipSignature(IReadOnlyList<MarkerDenseAreaPresentationNode> nodes)
        {
            var result = string.Empty;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (i > 0) result += ";";
                result += nodes[i].StableKey.ToString(CultureInfo.InvariantCulture) + ":" + nodes[i].MemberFingerprint;
            }
            return result;
        }

        private int IndexOfCluster(long key)
        {
            var low = 0;
            var high = _orderedClusters.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var candidate = _orderedClusters[mid].StableKey;
                if (candidate == key) return mid;
                if (candidate < key) low = mid + 1;
                else high = mid - 1;
            }
            return -1;
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        private static void Union(int[] parent, int left, int right)
        {
            var a = Find(parent, left);
            var b = Find(parent, right);
            if (a == b) return;
            if (a < b) parent[b] = a;
            else parent[a] = b;
        }

        private sealed class MutableAggregate
        {
            public MutableAggregate(string itemIdentity, string localizedName, MarkerSemanticCategory category, MarkerLifetimeKind lifetime)
            {
                ItemIdentity = itemIdentity;
                LocalizedName = localizedName;
                Category = category;
                Lifetime = lifetime;
            }
            public string ItemIdentity { get; }
            public string LocalizedName { get; }
            public MarkerSemanticCategory Category { get; }
            public MarkerLifetimeKind Lifetime { get; }
            public int Count { get; set; }
        }
    }
}
