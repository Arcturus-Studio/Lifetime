using System;
using System.Diagnostics;

namespace TwistedOak.Util {
    /// <summary>
    /// Runs callbacks when transitioning permanently from mortal to either dead or immortal.
    /// The default lifetime is immortal.
    /// Lifetimes whose source is garbage collected are neither dead nor immortal: they are in mortal limbo and will discard all callbacks.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    public struct Lifetime {
        /// <summary>
        /// The default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to immortal.
        /// </summary>
        public static readonly Lifetime Immortal = default(Lifetime);
        /// <summary>
        /// NOT the default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to dead.
        /// </summary>
        public static readonly Lifetime Dead = new Lifetime(SoulUtils.DeadSoul);

        private readonly ISoul _defSoul;
        internal ISoul Soul { get { return _defSoul ?? SoulUtils.ImmortalSoul; } }
        internal Lifetime(ISoul soul) {
            this._defSoul = soul;
        }

        /// <summary>Determines if this lifetime has not yet permanently transitioned from mortal to immortal or dead.</summary>
        public bool IsMortal { get { return Soul.Phase.IsMortal(); } }
        /// <summary>Determines if this lifetime has permanently transitioned from mortal to immortal.</summary>
        public bool IsImmortal { get { return Soul.Phase.IsImmortal(); } }
        /// <summary>Determines if this lifetime has permanently transitioned from mortal to dead.</summary>
        public bool IsDead { get { return Soul.Phase.IsDead(); } }

        /// <summary>
        /// Registers an action to perform when this lifetime is either dead or immortal.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead or immortal, the registration is cancelled.
        /// </summary>
        public void WhenNotMortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            Soul.WhenNotMortal(action, registrationLifetime.Soul);
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is dead.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead, the registration is cancelled.
        /// </summary>
        public void WhenDead(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            Soul.WhenDead(action, registrationLifetime.Soul);
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is immortal.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes immortal, the registration is cancelled.
        /// </summary>
        public void WhenImmortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            Soul.WhenImmortal(action, registrationLifetime.Soul);
        }

        ///<summary>Returns a text representation of the lifetime's current state.</summary>
        public override string ToString() {
            return IsImmortal ? "Immortal"
                 : IsDead ? "Dead"
                 : this.Soul.Phase == Phase.MortalLimbo ? "Alive (Limbo)"
                 : "Alive";
        }
    }
}
