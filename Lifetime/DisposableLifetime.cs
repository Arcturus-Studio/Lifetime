using System;
using System.Diagnostics;

namespace TwistedOak.Util {
    /// <summary>
    /// Exposes a lifetime that permanently transitions from mortal to dead when the managing DisposableLifetime instance is disposed.
    /// When a DisposableLifetime is garbage collected without being disposed, the exposed lifetime becomes immortal.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    public sealed class DisposableLifetime : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        ///<summary>The lifetime that transitions from mortal to dead when the managing DisposableLifetime is disposed.</summary>
        public Lifetime Lifetime { get { return _source.Lifetime; } }
        ///<summary>Transitions the exposed lifetime from mortal to dead.</summary>
        public void Dispose() {
            _source.EndLifetime();
        }
        ///<summary>Returns a text representation of the disposable lifetime's current state.</summary>
        public override string ToString() {
            return _source.ToString();
        }
    }
}
