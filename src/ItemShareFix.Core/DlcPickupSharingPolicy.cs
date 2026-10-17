using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    public enum DlcPickupKind { Other, Hallowed, Hex, Page }

    public readonly struct DlcSharingSettings
    {
        public DlcSharingSettings(bool hallowed, bool hex, bool pages)
        {
            Hallowed = hallowed;
            Hex = hex;
            Pages = pages;
        }

        public bool Hallowed { get; }
        public bool Hex { get; }
        public bool Pages { get; }

        public bool Allows(DlcPickupKind kind)
            => kind == DlcPickupKind.Hallowed ? Hallowed
                : kind == DlcPickupKind.Hex ? Hex
                : kind == DlcPickupKind.Page && Pages;
    }

    public static class DlcPickupSharingPolicy
    {
        public static bool CanShare(DlcPickupKind kind, DlcSharingSettings settings,
            bool hidden, bool consumed, bool worldUnique, bool objectiveRelated,
            bool progressionExcluded, bool activePageSpell)
            => settings.Allows(kind) && !hidden && !consumed && !worldUnique
                && !objectiveRelated && !progressionExcluded
                && (kind != DlcPickupKind.Page || activePageSpell);
    }

    // Per-call context, never a global tier override. Nested pickup calls shadow
    // their parent and disposal restores it even when an upstream grant throws.
    public sealed class SharedInteractionContext<T> where T : class
    {
        private readonly Stack<Frame> _frames = new Stack<Frame>();

        private sealed class Frame : IDisposable
        {
            private readonly SharedInteractionContext<T> _owner;
            public Frame(SharedInteractionContext<T> owner, T? key, bool alreadyOwned)
            { _owner = owner; Key = key; AlreadyOwned = alreadyOwned; }
            public T? Key { get; }
            public bool AlreadyOwned { get; }
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                if (_owner._frames.Count == 0 || !ReferenceEquals(_owner._frames.Peek(), this))
                    throw new InvalidOperationException("Shared pickup scope disposal order mismatch.");
                _owner._frames.Pop();
                _disposed = true;
            }
        }

        public IDisposable Enter(T? key, bool alreadyOwned)
        {
            var frame = new Frame(this, key, alreadyOwned);
            _frames.Push(frame);
            return frame;
        }

        public bool IsOwned(T key)
            => _frames.Count > 0 && _frames.Peek().AlreadyOwned
                && ReferenceEquals(_frames.Peek().Key, key);
    }

    public sealed class SharedOptionPolicy<T> where T : class
    {
        private readonly HashSet<T> _allowed = new HashSet<T>();
        public IEnumerable<T> Allowed => _allowed;
        public void Clear() => _allowed.Clear();
        public bool Contains(T option) => _allowed.Contains(option);
        public void Observe(T option, bool alreadyShared, bool currentlyShareable)
        {
            if (currentlyShareable) _allowed.Add(option);
            else if (!alreadyShared) _allowed.Remove(option);
        }
    }
}
