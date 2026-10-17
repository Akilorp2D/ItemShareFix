using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public readonly struct MarkerScreenCandidate
    {
        public MarkerScreenCandidate(long key, float x, float y, int totalCount)
        { Key = key; X = x; Y = y; TotalCount = totalCount; }
        public long Key { get; }
        public float X { get; }
        public float Y { get; }
        public int TotalCount { get; }
    }

    public sealed class MarkerScreenGroup
    {
        internal MarkerScreenGroup(int cell) => Cell = cell;
        public int Cell { get; }
        public IReadOnlyList<long> Keys => Members;
        internal List<long> Members { get; } = new List<long>(MarkerPresentationPolicy.MaxLogicalMarkers);
    }

    /// <summary>Only presentation grouping. Never changes physical clusters, eligibility or inventory.</summary>
    public sealed class MarkerScreenDeclutterTracker
    {
        public const int DefaultCardLimit = 8;
        public const int MinimumCardLimit = 4;
        public const int MaximumCardLimit = 16;
        public const float CellHysteresis = 0.035f;
        private readonly MarkerScreenGroup[] _cells = new MarkerScreenGroup[MaximumCardLimit];
        private readonly List<MarkerScreenGroup> _groups = new List<MarkerScreenGroup>(MaximumCardLimit);
        private readonly Dictionary<long, int> _lastCell = new Dictionary<long, int>();
        private readonly HashSet<long> _seen = new HashSet<long>();
        private readonly List<long> _stale = new List<long>();
        private readonly List<MarkerScreenCandidate> _valid = new List<MarkerScreenCandidate>(MarkerPresentationPolicy.MaxLogicalMarkers);
        private ulong _signature;
        private int _limit;
        public MarkerScreenDeclutterTracker()
        { for (var i = 0; i < _cells.Length; i++) _cells[i] = new MarkerScreenGroup(i); }
        public bool Active { get; private set; }
        public bool MembershipChanged { get; private set; }
        public IReadOnlyList<MarkerScreenGroup> Groups => _groups;
        public static int ClampLimit(int value) => Math.Max(MinimumCardLimit, Math.Min(MaximumCardLimit, value));
        public static MarkerPresentationSettings BuildSettings(MarkerPresentationSettings original, bool active, bool expanded)
            => !active ? original : new MarkerPresentationSettings(
                expanded ? MarkerPresentationMode.Detailed : MarkerPresentationMode.Compact,
                expanded && original.ShowDistance, original.Scale, Math.Min(4, original.DetailRows),
                original.ShowCategoryDiamond, original.ShowTierComposition, true,
                MarkerCompactMixedStyle.CategoryDiamonds, original.CategorySortOrder);

        public void Update(IReadOnlyList<MarkerScreenCandidate> candidates, bool enabled, int cardLimit)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var limit = ClampLimit(cardLimit);
            _valid.Clear(); _seen.Clear(); _groups.Clear();
            for (var i = 0; i < _cells.Length; i++) _cells[i].Members.Clear();
            long total = 0;
            for (var i = 0; i < candidates.Count && i < MarkerPresentationPolicy.MaxLogicalMarkers; i++)
            {
                var c = candidates[i];
                if (!Finite(c.X) || !Finite(c.Y) || c.TotalCount <= 0 || !_seen.Add(c.Key)) continue;
                _valid.Add(c); total += c.TotalCount;
            }
            var active = enabled && (_valid.Count > limit || total > limit * 2L
                || (Active && (_valid.Count > Math.Max(0, limit - 2) || total > Math.Max(0, limit * 2L - 2))));
            if (_limit != limit) _lastCell.Clear();
            var columns = limit >= 8 ? 4 : 2;
            var rows = (limit + columns - 1) / columns;
            if (active)
            {
                for (var i = 0; i < _valid.Count; i++)
                {
                    var c = _valid[i];
                    var x = Math.Max(0f, Math.Min(1f, c.X));
                    var y = Math.Max(0f, Math.Min(1f, c.Y));
                    var cell = Math.Min(limit - 1, Math.Min(rows - 1, (int)(y * rows)) * columns
                        + Math.Min(columns - 1, (int)(x * columns)));
                    if (_lastCell.TryGetValue(c.Key, out var previous))
                    {
                        var left = (previous % columns) / (float)columns;
                        var right = previous == limit - 1 ? 1f : left + 1f / columns;
                        var bottom = (previous / columns) / (float)rows;
                        if (x >= left - CellHysteresis && x <= right + CellHysteresis
                            && y >= bottom - CellHysteresis && y <= bottom + 1f / rows + CellHysteresis) cell = previous;
                    }
                    _lastCell[c.Key] = cell;
                    _cells[cell].Members.Add(c.Key);
                }
            }
            else _lastCell.Clear();
            _stale.Clear();
            foreach (var key in _lastCell.Keys) if (!_seen.Contains(key)) _stale.Add(key);
            for (var i = 0; i < _stale.Count; i++) _lastCell.Remove(_stale[i]);
            ulong signature = active ? 14695981039346656037UL : 0;
            for (var i = 0; i < limit; i++)
            {
                var group = _cells[i];
                if (group.Members.Count == 0) continue;
                group.Members.Sort(); _groups.Add(group);
                signature = (signature ^ (ulong)i) * 1099511628211UL;
                for (var k = 0; k < group.Members.Count; k++)
                    signature = (signature ^ unchecked((ulong)group.Members[k])) * 1099511628211UL;
            }
            MembershipChanged = signature != _signature || Active != active || limit != _limit;
            _signature = signature; Active = active; _limit = limit;
        }

        public void Clear()
        {
            _groups.Clear(); _lastCell.Clear(); _seen.Clear(); _stale.Clear(); _valid.Clear();
            for (var i = 0; i < _cells.Length; i++) _cells[i].Members.Clear();
            _signature = 0; _limit = 0; Active = false; MembershipChanged = false;
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
