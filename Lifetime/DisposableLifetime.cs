using System;

namespace TwistedOak.Util {
    /// <summary>
    /// Exposes a lifetime that permanently transitions from mortal to dead when the managing DisposableLifetime instance is disposed.
    /// When a DisposableLifetime is garbage collected without being disposed, the exposed lifetime becomes stuck in mortal limbo.
    /// </summary>
    public sealed class DisposableLifetime : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        ///<summary>The lifetime that transitions from mortal to dead when the managing DisposableLifetime is disposed.</summary>
        public Lifetime Lifetime { get { return this._source.Lifetime; } }
        ///<summary>Transitions the exposed lifetime from mortal to dead.</summary>
        public void Dispose() {
            _source.EndLifetime();
        }
    }
}
