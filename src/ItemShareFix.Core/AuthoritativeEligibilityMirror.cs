using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    // Presentation only. A snapshot can neither create nor complete a gameplay claim.
    public sealed class AuthoritativeEligibilityMirror<TOwner, TOption> where TOwner : class where TOption : class
    {
        private sealed class Entry
        {
            public Entry(TOwner owner) { Owner = new WeakReference<TOwner>(owner); }
            public WeakReference<TOwner> Owner { get; }
            public uint Request;
            public double NextRequest;
            public bool HasSnapshot;
            public HashSet<TOption> Allowed { get; } = new HashSet<TOption>();
            public HashSet<uint> Collectors { get; } = new HashSet<uint>();
        }
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _stale = new List<int>();
        private uint _sequence;
        public int Count => _entries.Count;
        public uint Request(int id, TOwner owner, double now)
        {
            if (!_entries.TryGetValue(id, out var entry) || !Matches(entry, owner))
                _entries[id] = entry = new Entry(owner);
            if (now < entry.NextRequest) return 0;
            entry.NextRequest = now + 1d;
            if (entry.Request != 0) return entry.Request; // Retry the same request: RTT > 1s must not starve replies.
            if (++_sequence == 0) ++_sequence;
            return entry.Request = _sequence;
        }
        public bool Apply(int id, TOwner owner, uint request, IEnumerable<TOption> allowed, IEnumerable<uint>? collectors = null)
        {
            if (request == 0 || !_entries.TryGetValue(id, out var entry) || !Matches(entry, owner)
                || entry.Request != request) return false;
            entry.Allowed.Clear();
            foreach (var option in allowed) entry.Allowed.Add(option);
            entry.Collectors.Clear();
            if (collectors != null) foreach (var master in collectors) if (master != 0) entry.Collectors.Add(master);
            entry.HasSnapshot = true;
            entry.Request = 0; // Consume the reply; replay cannot replace a newer accepted state.
            return true;
        }
        public bool Contains(int id, TOwner owner, TOption option)
            => _entries.TryGetValue(id, out var entry) && Matches(entry, owner) && entry.Allowed.Contains(option);
        public bool HasRetainedOptions(int id, TOwner owner)
            => _entries.TryGetValue(id, out var entry) && Matches(entry, owner) && entry.HasSnapshot && entry.Allowed.Count > 0;
        public bool TryCollected(int id, TOwner owner, uint master, out bool collected)
        {
            collected = false;
            if (master == 0 || !_entries.TryGetValue(id, out var entry) || !Matches(entry, owner) || !entry.HasSnapshot) return false;
            collected = entry.Collectors.Contains(master);
            return true;
        }
        private static bool Matches(Entry entry, TOwner owner)
            => entry.Owner.TryGetTarget(out var previous) && ReferenceEquals(previous, owner);
        public void Prune(Func<TOwner, bool> live)
        {
            _stale.Clear();
            foreach (var pair in _entries)
                if (!pair.Value.Owner.TryGetTarget(out var owner) || !live(owner)) _stale.Add(pair.Key);
            foreach (var id in _stale) _entries.Remove(id);
        }
        public void Clear() { _entries.Clear(); _stale.Clear(); } // Keep sequence: old replies cannot match a new request.
    }
}
