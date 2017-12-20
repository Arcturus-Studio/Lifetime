using System;
using System.Diagnostics;
using System.Threading;
using TwistedOak.Util.Soul;

namespace TwistedOak.Util {
    /// <summary>
    /// Runs callbacks when transitioning permanently from mortal to either dead or immortal.
    /// The default lifetime is immortal.
    /// Lifetimes whose source is garbage collected are immortal.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    public struct Lifetime : IEquatable<Lifetime> {
        /// <summary>
        /// The default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to immortal.
        /// </summary>
        public static readonly Lifetime Immortal = new Lifetime(ImmortalSoul.Instance);
        /// <summary>
        /// NOT the default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to dead.
        /// </summary>
        public static readonly Lifetime Dead = new Lifetime(DeadSoul.Instance);

        //Must default to ImmortalSould.Instance in property rather than using auto-prop and setting default
        //in contructor so that default(Lifetime).Soul is ImmortalSoul.Instance not null
        private readonly ISoul _defSoul;
        internal ISoul Soul => _defSoul ?? ImmortalSoul.Instance;
        internal Lifetime(ISoul soul) {
            _defSoul = soul;
        }

        ///<summary>Determines if this lifetime is still transiently mortal.</summary>
        public bool IsMortal => Soul.Phase == Phase.Mortal;
        ///<summary>Determines if this lifetime is permanently immortal.</summary>
        public bool IsImmortal => Soul.Phase == Phase.Immortal;
        ///<summary>Determines if this lifetime is permanently dead.</summary>
        public bool IsDead => Soul.Phase == Phase.Dead;

        /// <summary>
        /// Registers an action to perform when this lifetime is dead.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead, the registration is cancelled.
        /// If the lifetime is already dead, the callback is run synchronously.
        /// </summary>
        /// <param name="action">
        /// The callback to be run when the lifetime is dead.
        /// </param>
        /// <param name="registrationLifetime">
        /// Determines when/if the WhenDead callback registration is cancelled, meaning the callback will not be run.
        /// The registration is cancelled when the registration lifetime dies.
        /// Defaults to an immortal lifetime.
        /// </param>
        public void WhenDead(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var s = Soul;
            s.DependentRegister(
                () => { if (s.Phase == Phase.Dead) action(); },
                registrationLifetime.Soul);
        }

        /// <summary>
        /// Determines if two lifetimes are guaranteed to be in the same phase from now on.
        /// Mortal lifetimes are only congruent if they have the same source.
        /// All immortal lifetimes are congruent.
        /// All dead lifetimes are congruent.
        /// Two initially non-congruent lifetimes can become congruent by ending up in the same non-mortal state.
        /// </summary>
        /// <param name="other">The lifetime that this lifetime is being compared to.</param>
        public bool IsCongruentTo(Lifetime other) {
            if (Equals(this, other)) return true;
            var consistentPhase = Soul.Phase;
            return consistentPhase != Phase.Mortal && consistentPhase == other.Soul.Phase;
        }

        ///<summary>Returns a cancellation token that is cancelled when the lifetime ends.</summary>
        /// <remarks>
        /// Technically this should be an explicit conversion, because cancellation tokens curently don't handle 'becoming immotal'.
        /// A lifetime converted to a token and back will be stuck mortal if the original becomes immortal, instead of properly tracking it.
        /// However, this often only affects garbage collection instead of visible behavior, and interop with tokens should be painless.
        /// I think the trade-off is worth it.
        /// </remarks>
        public static implicit operator CancellationToken(Lifetime lifetime) {
            if (lifetime.IsImmortal) return default(CancellationToken);
            
            var source = new CancellationTokenSource();
            lifetime.WhenDead(source.Cancel);
            return source.Token;
        }
        ///<summary>Returns a lifetime that ends when the CancellationToken is cancelled.</summary>
        public static implicit operator Lifetime(CancellationToken token) {
            if (!token.CanBeCanceled) return Immortal;
            if (token.IsCancellationRequested) return Dead;
            
            var source = new LifetimeSource();
            token.Register(source.EndLifetime);
            return source.Lifetime;
        }

        /// <summary>Determines if the other lifetime has the same source.</summary>
        /// <param name="other">The lifetime that this lifetime is being compared to.</param>
        public bool Equals(Lifetime other) => Equals(Soul, other.Soul);
        ///<summary>Returns the hash code for this lifetime, based on its source.</summary>
        public override int GetHashCode() => Soul.GetHashCode();
        ///<summary>Determines if the other object is a lifetime with the same source.</summary>
        public override bool Equals(object obj) => obj is Lifetime && Equals((Lifetime)obj);
        ///<summary>Returns a text representation of the lifetime's current state.</summary>
        public override string ToString() {
            if (Soul.Phase == Phase.Mortal) return "Alive";
            return Soul.Phase.ToString();
        }
    }
}
