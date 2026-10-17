using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public sealed class SharedEligibilityCache<TOwner, TOption> where TOwner : class where TOption : class
    {
        public sealed class Entry
        {
            internal Entry(TOwner owner) { Owner = new WeakReference<TOwner>(owner); }
            public WeakReference<TOwner> Owner { get; }
            public SharedOptionPolicy<TOption> Options { get; } = new SharedOptionPolicy<TOption>();
        }
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _stale = new List<int>();
        private object? _epoch;
        public int Count => _entries.Count;
        public bool EnsureEpoch(object? epoch)
        {
            if (ReferenceEquals(epoch, _epoch)) return false;
            _epoch = epoch;
            Clear();
            return true;
        }
        public Entry For(int id, TOwner owner, bool alreadyShared)
        {
            if (!TryGet(id, owner, out var entry)) _entries[id] = entry = new Entry(owner);
            if (!alreadyShared) entry.Options.Clear();
            return entry;
        }
        public bool TryGet(int id, TOwner owner, out Entry entry)
        {
            if (_entries.TryGetValue(id, out var found) && found.Owner.TryGetTarget(out var previous)
                && ReferenceEquals(previous, owner)) { entry = found; return true; }
            entry = null!;
            return false;
        }
        public bool TryGetLive(int id, Func<TOwner, bool> live, out Entry entry)
        {
            if (_entries.TryGetValue(id, out var found) && found.Owner.TryGetTarget(out var owner) && live(owner))
            { entry = found; return true; }
            entry = null!;
            return false;
        }
        public void Remove(int id) => _entries.Remove(id);
        public void Clear() { _entries.Clear(); _stale.Clear(); }
        public void Prune(Func<TOwner, bool> live)
        {
            _stale.Clear();
            foreach (var pair in _entries)
                if (!pair.Value.Owner.TryGetTarget(out var owner) || !live(owner)) _stale.Add(pair.Key);
            foreach (var id in _stale) _entries.Remove(id);
        }
    }
}
