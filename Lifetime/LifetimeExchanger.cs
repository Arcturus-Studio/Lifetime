using System;
using System.Threading;

namespace TwistedOak.Util {
    public sealed class LifetimeExchanger : IDisposable {
        private LifetimeSource _active;
        public Lifetime StartNextAndEndPrevious() {
            var next = new LifetimeSource();
            var prev = Interlocked.Exchange(ref this._active, next);
            if (prev != null) prev.EndLifetime();
            return next.Lifetime;
        }
        public void EndPrevious() {
            var prev = Interlocked.Exchange(ref this._active, null);
            if (prev != null) prev.EndLifetime();
        }
        public void Dispose() {
            this._active.GiveEternalLifetime();
        }
    }
}