using System;
using System.Collections.Generic;

namespace ItemShareFix.Core
{
    // The returned lease owns exactly one frame, even if a nested Begin is rejected.
    public sealed class OwnedContextStack<T> where T : class
    {
        private readonly Stack<Lease> _frames = new Stack<Lease>();
        public int Count => _frames.Count;
        public T Peek() => _frames.Peek().Value;
        public Lease Enter(T value, Action<T>? initialize = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var lease = new Lease(this, value);
            _frames.Push(lease);
            try { initialize?.Invoke(value); }
            catch { lease.Dispose(); throw; }
            return lease;
        }
        public void Clear()
        {
            while (_frames.Count > 0) _frames.Pop().Retire();
        }

        public sealed class Lease : IDisposable
        {
            private readonly OwnedContextStack<T> _owner;
            private bool _closed;
            internal Lease(OwnedContextStack<T> owner, T value) { _owner = owner; Value = value; }
            public T Value { get; }
            internal void Retire() => _closed = true;
            public void Complete(Action<T>? completion)
            {
                if (_closed) return;
                if (_owner._frames.Count == 0 || !ReferenceEquals(_owner._frames.Peek(), this))
                    throw new InvalidOperationException("Distribution scope disposal order mismatch.");
                _owner._frames.Pop();
                _closed = true; // Finalizer cannot pop a parent if completion throws.
                completion?.Invoke(Value);
            }
            public void Dispose() => Complete(null);
        }
    }
}
