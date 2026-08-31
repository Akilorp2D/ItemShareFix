using System;
using System.Collections.Generic;
using System.Globalization;

namespace ItemShareFix.Core
{
    public readonly struct MarkerWorldPoint : IEquatable<MarkerWorldPoint>
    {
        public MarkerWorldPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool Equals(MarkerWorldPoint other)
            => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object? obj) => obj is MarkerWorldPoint other && Equals(other);
        public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString()
            => X.ToString("F2", CultureInfo.InvariantCulture) + ","
               + Y.ToString("F2", CultureInfo.InvariantCulture) + ","
               + Z.ToString("F2", CultureInfo.InvariantCulture);

        public static float DistanceSquared(MarkerWorldPoint left, MarkerWorldPoint right)
        {
            var dx = left.X - right.X;
            var dy = left.Y - right.Y;
            var dz = left.Z - right.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    public enum MarkerSemanticCategory
    {
        Tier1,
        Tier2,
        Tier3,
        Boss,
        Lunar,
        Void,
        Equipment,
        LunarEquipment,
        Other,
        Unknown,
        CommandState,
    }

    public static class MarkerSemanticCategoryPolicy
    {
        public static MarkerSemanticCategory From(PersonalMarkerKind markerKind, MarkerClassKind classKind)
        {
            if (markerKind == PersonalMarkerKind.CommandPicker && classKind == MarkerClassKind.Unknown)
                return MarkerSemanticCategory.CommandState;

            switch (classKind)
            {
                case MarkerClassKind.Tier1: return MarkerSemanticCategory.Tier1;
                case MarkerClassKind.Tier2: return MarkerSemanticCategory.Tier2;
                case MarkerClassKind.Tier3: return MarkerSemanticCategory.Tier3;
                case MarkerClassKind.Boss: return MarkerSemanticCategory.Boss;
                case MarkerClassKind.Lunar: return MarkerSemanticCategory.Lunar;
                case MarkerClassKind.Void: return MarkerSemanticCategory.Void;
                case MarkerClassKind.Equipment: return MarkerSemanticCategory.Equipment;
                case MarkerClassKind.LunarEquipment: return MarkerSemanticCategory.LunarEquipment;
                case MarkerClassKind.Other: return MarkerSemanticCategory.Other;
                default: return MarkerSemanticCategory.Unknown;
            }
        }

        public static int StableOrder(MarkerSemanticCategory category)
        {
            switch (category)
            {
                case MarkerSemanticCategory.Tier1: return 0;
                case MarkerSemanticCategory.Tier2: return 1;
                case MarkerSemanticCategory.Tier3: return 2;
                case MarkerSemanticCategory.Boss: return 3;
                case MarkerSemanticCategory.Lunar: return 4;
                case MarkerSemanticCategory.Void: return 5;
                case MarkerSemanticCategory.Equipment: return 6;
                case MarkerSemanticCategory.LunarEquipment: return 7;
                case MarkerSemanticCategory.Other: return 8;
                case MarkerSemanticCategory.Unknown: return 9;
                default: return 10;
            }
        }
    }

    public readonly struct MarkerWorldMember
    {
        public MarkerWorldMember(
            long stableKey,
            PersonalMarkerKind markerKind,
            MarkerWorldPoint worldPosition,
            string itemIdentity,
            string localizedName,
            MarkerClassKind classKind)
            : this(stableKey, markerKind, worldPosition, itemIdentity, localizedName, classKind, MarkerLifetimeKind.Permanent)
        {
        }

        public MarkerWorldMember(
            long stableKey,
            PersonalMarkerKind markerKind,
            MarkerWorldPoint worldPosition,
            string itemIdentity,
            string localizedName,
            MarkerClassKind classKind,
            MarkerLifetimeKind lifetime)
        {
            StableKey = stableKey;
            MarkerKind = markerKind;
            WorldPosition = worldPosition;
            ItemIdentity = string.IsNullOrWhiteSpace(itemIdentity)
                ? "UNKNOWN:" + stableKey.ToString(CultureInfo.InvariantCulture)
                : itemIdentity;
            LocalizedName = string.IsNullOrWhiteSpace(localizedName) ? "Pickup" : localizedName;
            ClassKind = classKind;
            Category = MarkerSemanticCategoryPolicy.From(markerKind, classKind);
            Lifetime = lifetime;
        }

        public long StableKey { get; }
        public PersonalMarkerKind MarkerKind { get; }
        public MarkerWorldPoint WorldPosition { get; }
        public string ItemIdentity { get; }
        public string LocalizedName { get; }
        public MarkerClassKind ClassKind { get; }
        public MarkerSemanticCategory Category { get; }
        public MarkerLifetimeKind Lifetime { get; }
    }

    public readonly struct MarkerCategoryCount
    {
        public MarkerCategoryCount(MarkerSemanticCategory category, int count)
        {
            Category = category;
            Count = Math.Max(0, count);
        }

        public MarkerSemanticCategory Category { get; }
        public int Count { get; }
    }

    public readonly struct MarkerItemAggregate
    {
        public MarkerItemAggregate(string itemIdentity, string localizedName, MarkerSemanticCategory category, int count)
            : this(itemIdentity, localizedName, category, count, MarkerLifetimeKind.Permanent)
        {
        }

        public MarkerItemAggregate(
            string itemIdentity,
            string localizedName,
            MarkerSemanticCategory category,
            int count,
            MarkerLifetimeKind lifetime)
        {
            ItemIdentity = itemIdentity ?? string.Empty;
            LocalizedName = string.IsNullOrWhiteSpace(localizedName) ? "Pickup" : localizedName;
            Category = category;
            Count = Math.Max(0, count);
            Lifetime = lifetime;
        }

        public string ItemIdentity { get; }
        public string LocalizedName { get; }
        public MarkerSemanticCategory Category { get; }
        public int Count { get; }
        public MarkerLifetimeKind Lifetime { get; }
    }

    public sealed class MarkerSemanticCluster
    {
        internal MarkerSemanticCluster(
            long stableKey,
            string memberFingerprint,
            MarkerWorldPoint worldAnchor,
            long[] memberStableKeys,
            MarkerCategoryCount[] composition,
            MarkerItemAggregate[] itemRows)
            : this(stableKey, memberFingerprint, worldAnchor, memberStableKeys, composition, itemRows, 0, 0, 0)
        {
        }

        internal MarkerSemanticCluster(
            long stableKey,
            string memberFingerprint,
            MarkerWorldPoint worldAnchor,
            long[] memberStableKeys,
            MarkerCategoryCount[] composition,
            MarkerItemAggregate[] itemRows,
            int temporaryPhysicalMemberCount,
            int mixedLifetimeMemberCount,
            int unknownLifetimeMemberCount)
        {
            StableKey = stableKey;
            MemberFingerprint = memberFingerprint ?? string.Empty;
            WorldAnchor = worldAnchor;
            MemberStableKeys = memberStableKeys ?? Array.Empty<long>();
            Composition = composition ?? Array.Empty<MarkerCategoryCount>();
            ItemRows = itemRows ?? Array.Empty<MarkerItemAggregate>();
            TemporaryPhysicalMemberCount = Math.Max(0, temporaryPhysicalMemberCount);
            MixedLifetimeMemberCount = Math.Max(0, mixedLifetimeMemberCount);
            UnknownLifetimeMemberCount = Math.Max(0, unknownLifetimeMemberCount);
        }

        public long StableKey { get; }
        public string MemberFingerprint { get; }
        public MarkerWorldPoint WorldAnchor { get; private set; }
        public IReadOnlyList<long> MemberStableKeys { get; }
        public IReadOnlyList<MarkerCategoryCount> Composition { get; }
        public IReadOnlyList<MarkerItemAggregate> ItemRows { get; }
        public int TotalCount => MemberStableKeys.Count;
        public int TemporaryPhysicalMemberCount { get; }
        public int MixedLifetimeMemberCount { get; }
        public int UnknownLifetimeMemberCount { get; }
        public int PermanentPhysicalMemberCount
            => Math.Max(0, TotalCount - TemporaryPhysicalMemberCount - MixedLifetimeMemberCount - UnknownLifetimeMemberCount);
        public bool HasTemporaryLifetime => TemporaryPhysicalMemberCount > 0 || MixedLifetimeMemberCount > 0;
        public MarkerLifetimeKind LifetimeSummary
            => MarkerLifetimePolicy.SummarizeCluster(TotalCount, TemporaryPhysicalMemberCount, MixedLifetimeMemberCount, UnknownLifetimeMemberCount);
        public bool IsMixedCategory => Composition.Count > 1;
        public bool IsHomogeneousCategory => Composition.Count == 1;
        public MarkerSemanticCategory HomogeneousCategory
            => Composition.Count == 1 ? Composition[0].Category : MarkerSemanticCategory.Unknown;

        internal void RefreshWorldAnchor(MarkerWorldPoint worldAnchor)
            => WorldAnchor = worldAnchor;
    }

    public enum MarkerSemanticLifecycleKind
    {
        Created,
        MembershipChanged,
        Merged,
        Split,
        CompositionChanged,
        Removed,
    }

    public readonly struct MarkerSemanticLifecycleEvent
    {
        public MarkerSemanticLifecycleEvent(MarkerSemanticLifecycleKind kind, MarkerSemanticCluster cluster, string reason)
        {
            Kind = kind;
            Cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            Reason = reason ?? string.Empty;
        }

        public MarkerSemanticLifecycleKind Kind { get; }
        public MarkerSemanticCluster Cluster { get; }
        public string Reason { get; }
    }

    public sealed class MarkerSemanticUpdate
    {
        internal MarkerSemanticUpdate(IReadOnlyList<MarkerSemanticCluster> clusters, IReadOnlyList<MarkerSemanticLifecycleEvent> lifecycleEvents, bool membershipChanged)
        {
            Clusters = clusters;
            LifecycleEvents = lifecycleEvents;
            MembershipChanged = membershipChanged;
        }

        public IReadOnlyList<MarkerSemanticCluster> Clusters { get; }
        public IReadOnlyList<MarkerSemanticLifecycleEvent> LifecycleEvents { get; }
        public bool MembershipChanged { get; }
    }

    /// <summary>
    /// Semantic owner for marker-world clustering. Membership is based only on stable logical identity and world-space
    /// relation. Camera angle, FOV, projection coordinates, screen overlap and representative distance are deliberately
    /// absent from the API. Pair-link hysteresis prevents threshold flapping while add/collect/despawn is applied
    /// immediately on the next semantic update.
    /// </summary>
    public sealed class MarkerWorldClusterTracker
    {
        public const float MergeRadiusMeters = 4.50f;
        public const float SplitRadiusMeters = 6.00f;
        public const double ThresholdTransitionDwellSeconds = 0.35;
        public const double RecommendedSemanticSolveIntervalSeconds = 0.20;

        private readonly struct PairKey : IEquatable<PairKey>
        {
            public PairKey(long first, long second)
            {
                if (first <= second) { A = first; B = second; }
                else { A = second; B = first; }
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

        private readonly Dictionary<PairKey, PairState> _pairStates = new Dictionary<PairKey, PairState>();
        private readonly List<MarkerWorldMember> _ordered = new List<MarkerWorldMember>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<long> _lastMemberKeys = new List<long>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<ulong> _lastSemanticTokens = new List<ulong>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private readonly List<MarkerSemanticCluster> _clusters = new List<MarkerSemanticCluster>();
        private readonly List<MarkerSemanticCluster> _previousClusters = new List<MarkerSemanticCluster>();
        private readonly List<MarkerSemanticLifecycleEvent> _events = new List<MarkerSemanticLifecycleEvent>();
        private readonly List<PairKey> _stalePairs = new List<PairKey>();

        public IReadOnlyList<MarkerSemanticCluster> Clusters => _clusters;

        public MarkerSemanticUpdate Update(IReadOnlyList<MarkerWorldMember> members, double nowSeconds)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) nowSeconds = 0d;

            _ordered.Clear();
            var count = Math.Min(members.Count, MarkerPresentationPolicy.MaxLogicalMarkers);
            for (var i = 0; i < count; i++)
            {
                var candidate = members[i];
                if (!IsFinite(candidate.WorldPosition)) continue;
                _ordered.Add(candidate);
            }
            _ordered.Sort(WorldMemberComparer.Instance);
            DeduplicateOrderedStableKeys(_ordered);

            var memberSetChanged = MemberSetChanged(_ordered, _lastMemberKeys);
            var semanticContentChanged = SemanticContentChanged(_ordered, _lastSemanticTokens);
            _previousClusters.Clear();
            for (var i = 0; i < _clusters.Count; i++) _previousClusters.Add(_clusters[i]);

            var pairTopologyChanged = false;
            if (memberSetChanged)
            {
                // Preserve hysteresis state for unchanged pairs. Only genuinely new pairs are initialized immediately.
                PrunePairStates(_ordered);
                ReconcileNewPairsImmediately(_ordered);
                pairTopologyChanged = UpdatePairLinksWithHysteresis(_ordered, nowSeconds);
            }
            else
            {
                pairTopologyChanged = UpdatePairLinksWithHysteresis(_ordered, nowSeconds);
            }

            var rebuildClusters = _clusters.Count == 0 || memberSetChanged || semanticContentChanged || pairTopologyChanged;
            if (rebuildClusters)
            {
                BuildClusters(_ordered, _pairStates, _clusters);
            }
            else
            {
                // Stable semantic membership/content: update only the current world anchor in-place. This keeps the
                // 5 Hz semantic solve allocation-free for a settled pile while render projection still follows every frame.
                RefreshClusterAnchors(_ordered, _clusters);
            }

            var membershipChanged = rebuildClusters && !SameClusterMembership(_previousClusters, _clusters);
            if (rebuildClusters) BuildLifecycleEvents(_previousClusters, _clusters, memberSetChanged, _events);
            else _events.Clear();

            _lastMemberKeys.Clear();
            _lastSemanticTokens.Clear();
            for (var i = 0; i < _ordered.Count; i++)
            {
                _lastMemberKeys.Add(_ordered[i].StableKey);
                _lastSemanticTokens.Add(MemberSemanticToken(_ordered[i]));
            }

            return new MarkerSemanticUpdate(_clusters, _events, membershipChanged);
        }

        public void Clear()
        {
            _pairStates.Clear();
            _ordered.Clear();
            _lastMemberKeys.Clear();
            _lastSemanticTokens.Clear();
            _clusters.Clear();
            _previousClusters.Clear();
            _events.Clear();
            _stalePairs.Clear();
        }

        public static long StableClusterKey(IReadOnlyList<long> sortedMemberKeys)
        {
            if (sortedMemberKeys == null) throw new ArgumentNullException(nameof(sortedMemberKeys));
            ulong hash = 14695981039346656037UL;
            for (var i = 0; i < sortedMemberKeys.Count; i++)
            {
                var value = unchecked((ulong)sortedMemberKeys[i]);
                for (var b = 0; b < 8; b++)
                {
                    hash ^= (byte)(value & 0xFFUL);
                    hash *= 1099511628211UL;
                    value >>= 8;
                }
            }
            var result = unchecked((long)(hash & 0x7FFFFFFFFFFFFFFFUL));
            return result == 0 ? 1 : result;
        }

        public static string MemberFingerprint(IReadOnlyList<long> sortedMemberKeys)
            => StableClusterKey(sortedMemberKeys).ToString("X16", CultureInfo.InvariantCulture)
               + ":" + (sortedMemberKeys?.Count ?? 0).ToString(CultureInfo.InvariantCulture);

        public static MarkerWorldPoint CurrentAnchor(IReadOnlyList<MarkerWorldMember> members)
        {
            if (members == null || members.Count == 0) return default;
            double x = 0d, y = 0d, z = 0d;
            var count = 0;
            for (var i = 0; i < members.Count; i++)
            {
                if (!IsFinite(members[i].WorldPosition)) continue;
                x += members[i].WorldPosition.X;
                y += members[i].WorldPosition.Y;
                z += members[i].WorldPosition.Z;
                count++;
            }
            if (count == 0) return default;
            return new MarkerWorldPoint((float)(x / count), (float)(y / count), (float)(z / count));
        }

        private void ReconcileNewPairsImmediately(IReadOnlyList<MarkerWorldMember> ordered)
        {
            var mergeSquared = MergeRadiusMeters * MergeRadiusMeters;
            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var key = new PairKey(ordered[i].StableKey, ordered[j].StableKey);
                    if (_pairStates.ContainsKey(key)) continue;
                    _pairStates[key] = new PairState
                    {
                        Linked = MarkerWorldPoint.DistanceSquared(ordered[i].WorldPosition, ordered[j].WorldPosition) <= mergeSquared,
                    };
                }
            }
        }

        private bool UpdatePairLinksWithHysteresis(IReadOnlyList<MarkerWorldMember> ordered, double nowSeconds)
        {
            var changed = false;
            var mergeSquared = MergeRadiusMeters * MergeRadiusMeters;
            var splitSquared = SplitRadiusMeters * SplitRadiusMeters;
            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var key = new PairKey(ordered[i].StableKey, ordered[j].StableKey);
                    if (!_pairStates.TryGetValue(key, out var state))
                    {
                        state = new PairState { Linked = MarkerWorldPoint.DistanceSquared(ordered[i].WorldPosition, ordered[j].WorldPosition) <= mergeSquared };
                        _pairStates[key] = state;
                        continue;
                    }

                    var distanceSquared = MarkerWorldPoint.DistanceSquared(ordered[i].WorldPosition, ordered[j].WorldPosition);
                    if (state.Linked)
                    {
                        if (distanceSquared <= splitSquared)
                        {
                            state.Pending = false;
                            continue;
                        }
                        if (AdvancePending(state, target: false, nowSeconds)) changed = true;
                    }
                    else
                    {
                        if (distanceSquared >= mergeSquared)
                        {
                            state.Pending = false;
                            continue;
                        }
                        if (AdvancePending(state, target: true, nowSeconds)) changed = true;
                    }
                }
            }
            return changed;
        }

        private static bool AdvancePending(PairState state, bool target, double nowSeconds)
        {
            if (!state.Pending || state.PendingTarget != target)
            {
                state.Pending = true;
                state.PendingTarget = target;
                state.PendingSince = nowSeconds;
                return false;
            }

            if (nowSeconds - state.PendingSince < ThresholdTransitionDwellSeconds) return false;
            state.Linked = target;
            state.Pending = false;
            return true;
        }

        private void PrunePairStates(IReadOnlyList<MarkerWorldMember> ordered)
        {
            _stalePairs.Clear();
            foreach (var pair in _pairStates.Keys)
            {
                if (!ContainsStableKey(ordered, pair.A) || !ContainsStableKey(ordered, pair.B)) _stalePairs.Add(pair);
            }
            for (var i = 0; i < _stalePairs.Count; i++) _pairStates.Remove(_stalePairs[i]);
        }

        private static bool ContainsStableKey(IReadOnlyList<MarkerWorldMember> ordered, long key)
        {
            var low = 0;
            var high = ordered.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var candidate = ordered[mid].StableKey;
                if (candidate == key) return true;
                if (candidate < key) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }

        private static void BuildClusters(
            IReadOnlyList<MarkerWorldMember> ordered,
            IReadOnlyDictionary<PairKey, PairState> pairStates,
            List<MarkerSemanticCluster> output)
        {
            output.Clear();
            if (ordered.Count == 0) return;

            var parent = new int[ordered.Count];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            foreach (var pair in pairStates)
            {
                if (!pair.Value.Linked) continue;
                var left = IndexOfStableKey(ordered, pair.Key.A);
                var right = IndexOfStableKey(ordered, pair.Key.B);
                if (left >= 0 && right >= 0) Union(parent, left, right);
            }

            var groups = new Dictionary<int, List<MarkerWorldMember>>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var group))
                {
                    group = new List<MarkerWorldMember>();
                    groups.Add(root, group);
                }
                group.Add(ordered[i]);
            }

            var built = new List<MarkerSemanticCluster>(groups.Count);
            foreach (var group in groups.Values) built.Add(BuildCluster(group));
            built.Sort((left, right) => left.StableKey.CompareTo(right.StableKey));
            output.AddRange(built);
        }

        private static MarkerSemanticCluster BuildCluster(List<MarkerWorldMember> members)
        {
            members.Sort(WorldMemberComparer.Instance);
            var keys = new long[members.Count];
            for (var i = 0; i < members.Count; i++) keys[i] = members[i].StableKey;

            var compositionMap = new Dictionary<MarkerSemanticCategory, int>();
            var itemMap = new Dictionary<MarkerItemLifetimeKey, MutableItemAggregate>();
            var temporaryPhysicalMemberCount = 0;
            var mixedLifetimeMemberCount = 0;
            var unknownLifetimeMemberCount = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                compositionMap.TryGetValue(member.Category, out var categoryCount);
                compositionMap[member.Category] = categoryCount + 1;

                if (member.Lifetime == MarkerLifetimeKind.Temporary) temporaryPhysicalMemberCount++;
                else if (member.Lifetime == MarkerLifetimeKind.Mixed) mixedLifetimeMemberCount++;
                else if (member.Lifetime == MarkerLifetimeKind.Unknown) unknownLifetimeMemberCount++;

                var aggregateKey = new MarkerItemLifetimeKey(member.ItemIdentity, member.Lifetime);
                if (!itemMap.TryGetValue(aggregateKey, out var aggregate))
                {
                    aggregate = new MutableItemAggregate(member.ItemIdentity, member.LocalizedName, member.Category, member.Lifetime);
                    itemMap.Add(aggregateKey, aggregate);
                }
                aggregate.Count++;
            }

            var composition = new List<MarkerCategoryCount>(compositionMap.Count);
            foreach (var pair in compositionMap) composition.Add(new MarkerCategoryCount(pair.Key, pair.Value));
            composition.Sort((left, right) => MarkerSemanticCategoryPolicy.StableOrder(left.Category).CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category)));

            var items = new List<MarkerItemAggregate>(itemMap.Count);
            foreach (var aggregate in itemMap.Values)
                items.Add(new MarkerItemAggregate(aggregate.ItemIdentity, aggregate.LocalizedName, aggregate.Category, aggregate.Count, aggregate.Lifetime));
            items.Sort(MarkerItemAggregateComparer.Instance);

            return new MarkerSemanticCluster(
                StableClusterKey(keys),
                MemberFingerprint(keys),
                CurrentAnchor(members),
                keys,
                composition.ToArray(),
                items.ToArray(),
                temporaryPhysicalMemberCount,
                mixedLifetimeMemberCount,
                unknownLifetimeMemberCount);
        }

        private static void BuildLifecycleEvents(
            IReadOnlyList<MarkerSemanticCluster> previous,
            IReadOnlyList<MarkerSemanticCluster> current,
            bool sourceSetChanged,
            List<MarkerSemanticLifecycleEvent> output)
        {
            output.Clear();
            if (previous.Count == 0)
            {
                for (var i = 0; i < current.Count; i++)
                    output.Add(new MarkerSemanticLifecycleEvent(MarkerSemanticLifecycleKind.Created, current[i], "initial-or-add"));
                return;
            }

            for (var i = 0; i < current.Count; i++)
            {
                var cluster = current[i];
                var previousIndex = FindByFingerprint(previous, cluster.MemberFingerprint);
                if (previousIndex >= 0)
                {
                    if (!SameSemanticComposition(previous[previousIndex], cluster))
                        output.Add(new MarkerSemanticLifecycleEvent(MarkerSemanticLifecycleKind.CompositionChanged, cluster, "semantic-composition-changed"));
                    continue;
                }
                var overlapCount = 0;
                MarkerSemanticCluster? singleOverlap = null;
                for (var p = 0; p < previous.Count; p++)
                {
                    if (!Overlaps(previous[p].MemberStableKeys, cluster.MemberStableKeys)) continue;
                    overlapCount++;
                    singleOverlap = previous[p];
                }

                var kind = overlapCount > 1
                    ? MarkerSemanticLifecycleKind.Merged
                    : overlapCount == 1 && CountCurrentOverlaps(current, singleOverlap!.MemberStableKeys) > 1
                        ? MarkerSemanticLifecycleKind.Split
                        : overlapCount == 1
                            ? MarkerSemanticLifecycleKind.MembershipChanged
                            : MarkerSemanticLifecycleKind.Created;
                output.Add(new MarkerSemanticLifecycleEvent(kind, cluster, sourceSetChanged ? "identity-lifecycle" : "world-hysteresis"));
            }

            for (var i = 0; i < previous.Count; i++)
            {
                var cluster = previous[i];
                if (FindByFingerprint(current, cluster.MemberFingerprint) >= 0) continue;
                var stillOverlaps = false;
                for (var c = 0; c < current.Count; c++)
                {
                    if (!Overlaps(cluster.MemberStableKeys, current[c].MemberStableKeys)) continue;
                    stillOverlaps = true;
                    break;
                }
                if (!stillOverlaps)
                    output.Add(new MarkerSemanticLifecycleEvent(MarkerSemanticLifecycleKind.Removed, cluster, sourceSetChanged ? "identity-lifecycle" : "world-hysteresis"));
            }
        }

        private static bool SameSemanticComposition(MarkerSemanticCluster left, MarkerSemanticCluster right)
        {
            if (left.TemporaryPhysicalMemberCount != right.TemporaryPhysicalMemberCount
                || left.MixedLifetimeMemberCount != right.MixedLifetimeMemberCount
                || left.UnknownLifetimeMemberCount != right.UnknownLifetimeMemberCount) return false;
            if (left.Composition.Count != right.Composition.Count || left.ItemRows.Count != right.ItemRows.Count) return false;
            for (var i = 0; i < left.Composition.Count; i++)
            {
                if (left.Composition[i].Category != right.Composition[i].Category || left.Composition[i].Count != right.Composition[i].Count)
                    return false;
            }
            for (var i = 0; i < left.ItemRows.Count; i++)
            {
                var a = left.ItemRows[i];
                var b = right.ItemRows[i];
                if (!string.Equals(a.ItemIdentity, b.ItemIdentity, StringComparison.Ordinal)
                    || !string.Equals(a.LocalizedName, b.LocalizedName, StringComparison.Ordinal)
                    || a.Category != b.Category
                    || a.Lifetime != b.Lifetime
                    || a.Count != b.Count)
                    return false;
            }
            return true;
        }

        private static int CountCurrentOverlaps(IReadOnlyList<MarkerSemanticCluster> current, IReadOnlyList<long> previousMembers)
        {
            var count = 0;
            for (var i = 0; i < current.Count; i++) if (Overlaps(current[i].MemberStableKeys, previousMembers)) count++;
            return count;
        }

        private static int FindByFingerprint(IReadOnlyList<MarkerSemanticCluster> clusters, string fingerprint)
        {
            for (var i = 0; i < clusters.Count; i++)
                if (string.Equals(clusters[i].MemberFingerprint, fingerprint, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static bool Overlaps(IReadOnlyList<long> left, IReadOnlyList<long> right)
        {
            var i = 0;
            var j = 0;
            while (i < left.Count && j < right.Count)
            {
                if (left[i] == right[j]) return true;
                if (left[i] < right[j]) i++;
                else j++;
            }
            return false;
        }

        private static bool SameClusterMembership(IReadOnlyList<MarkerSemanticCluster> left, IReadOnlyList<MarkerSemanticCluster> right)
        {
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
                if (FindByFingerprint(right, left[i].MemberFingerprint) < 0) return false;
            return true;
        }

        private static void RefreshClusterAnchors(IReadOnlyList<MarkerWorldMember> ordered, IReadOnlyList<MarkerSemanticCluster> clusters)
        {
            for (var c = 0; c < clusters.Count; c++)
            {
                var cluster = clusters[c];
                double x = 0d, y = 0d, z = 0d;
                var count = 0;
                for (var m = 0; m < cluster.MemberStableKeys.Count; m++)
                {
                    var index = IndexOfStableKey(ordered, cluster.MemberStableKeys[m]);
                    if (index < 0) continue;
                    var point = ordered[index].WorldPosition;
                    if (!IsFinite(point)) continue;
                    x += point.X; y += point.Y; z += point.Z; count++;
                }
                if (count > 0) cluster.RefreshWorldAnchor(new MarkerWorldPoint((float)(x / count), (float)(y / count), (float)(z / count)));
            }
        }

        private static bool SemanticContentChanged(IReadOnlyList<MarkerWorldMember> ordered, IReadOnlyList<ulong> previous)
        {
            if (ordered.Count != previous.Count) return true;
            for (var i = 0; i < ordered.Count; i++) if (MemberSemanticToken(ordered[i]) != previous[i]) return true;
            return false;
        }

        private static ulong MemberSemanticToken(MarkerWorldMember member)
        {
            ulong hash = 14695981039346656037UL;
            AddHash(ref hash, unchecked((ulong)member.StableKey));
            AddHash(ref hash, (ulong)(uint)member.MarkerKind);
            AddHash(ref hash, (ulong)(uint)member.ClassKind);
            AddHash(ref hash, (ulong)(uint)member.Lifetime);
            AddHash(ref hash, StableStringHash(member.ItemIdentity));
            AddHash(ref hash, StableStringHash(member.LocalizedName));
            return hash;
        }

        private static ulong StableStringHash(string value)
        {
            ulong hash = 14695981039346656037UL;
            if (value == null) return hash;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                hash ^= (byte)(ch & 0xFF); hash *= 1099511628211UL;
                hash ^= (byte)(ch >> 8); hash *= 1099511628211UL;
            }
            return hash;
        }

        private static void AddHash(ref ulong hash, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                hash ^= (byte)(value & 0xFFUL);
                hash *= 1099511628211UL;
                value >>= 8;
            }
        }

        private static bool MemberSetChanged(IReadOnlyList<MarkerWorldMember> ordered, IReadOnlyList<long> previous)
        {
            if (ordered.Count != previous.Count) return true;
            for (var i = 0; i < ordered.Count; i++) if (ordered[i].StableKey != previous[i]) return true;
            return false;
        }

        private static void DeduplicateOrderedStableKeys(List<MarkerWorldMember> ordered)
        {
            for (var i = ordered.Count - 1; i > 0; i--)
                if (ordered[i].StableKey == ordered[i - 1].StableKey) ordered.RemoveAt(i);
        }

        private static int IndexOfStableKey(IReadOnlyList<MarkerWorldMember> ordered, long stableKey)
        {
            var low = 0;
            var high = ordered.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);
                var value = ordered[mid].StableKey;
                if (value == stableKey) return mid;
                if (value < stableKey) low = mid + 1;
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

        private static bool IsFinite(MarkerWorldPoint point)
            => IsFinite(point.X) && IsFinite(point.Y) && IsFinite(point.Z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class MutableItemAggregate
        {
            public MutableItemAggregate(string itemIdentity, string localizedName, MarkerSemanticCategory category, MarkerLifetimeKind lifetime)
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

        private sealed class WorldMemberComparer : IComparer<MarkerWorldMember>
        {
            public static readonly WorldMemberComparer Instance = new WorldMemberComparer();
            public int Compare(MarkerWorldMember left, MarkerWorldMember right) => left.StableKey.CompareTo(right.StableKey);
        }

        private sealed class MarkerItemAggregateComparer : IComparer<MarkerItemAggregate>
        {
            public static readonly MarkerItemAggregateComparer Instance = new MarkerItemAggregateComparer();
            public int Compare(MarkerItemAggregate left, MarkerItemAggregate right)
            {
                var category = MarkerSemanticCategoryPolicy.StableOrder(left.Category).CompareTo(MarkerSemanticCategoryPolicy.StableOrder(right.Category));
                if (category != 0) return category;
                var name = string.Compare(left.LocalizedName, right.LocalizedName, StringComparison.Ordinal);
                if (name != 0) return name;
                var identity = string.Compare(left.ItemIdentity, right.ItemIdentity, StringComparison.Ordinal);
                if (identity != 0) return identity;
                return MarkerLifetimePolicy.DetailedSortRank(left.Lifetime).CompareTo(MarkerLifetimePolicy.DetailedSortRank(right.Lifetime));
            }
        }
    }
}
