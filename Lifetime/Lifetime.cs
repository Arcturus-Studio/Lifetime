﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TwistedOak.Util {
    /// <summary>
    /// Runs callbacks when transitioning permanently from mortal to either dead or immortal.
    /// The default lifetime is immortal.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    public struct Lifetime {
        private enum Phase {
            /// <summary>The lifetime has not yet been killed or immortalized.</summary>
            Mortal,
            /// <summary>The lifetime has been permanently killed.</summary>
            Dead,
            /// <summary>The lifetime has been permanently immortalized.</summary>
            Immortal,
            /// <summary>
            /// The lifetime's source was garbage collected before the lifetime was killed or immortalized.
            /// Callbacks will not be run, because what they reference may have been finalized.
            /// The lifetime will never be killed or immortalized: it is stuck between the two states.
            /// </summary>
            MortalLimbo
        }
        private sealed class Data {
            public List<Action> Callbacks;
            public Phase Phase;
        }

        /// <summary>
        /// The default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to immortal.
        /// </summary>
        public static readonly Lifetime Immortal = default(Lifetime);
        /// <summary>
        /// A lifetime that has already permanently transitioned from mortal to dead.
        /// </summary>
        public static readonly Lifetime Dead = new Lifetime(new Data {Phase = Phase.Dead});

        private readonly Data _data;
        private Lifetime(Data data) {
            this._data = data;
        }

        /// <summary>Determines if this lifetime has not yet permanently transitioned from mortal to immortal or dead.</summary>
        public bool IsMortal { get { return _data != null && (_data.Phase == Phase.Mortal || _data.Phase == Phase.MortalLimbo); } }
        /// <summary>Determines if this lifetime has permanently transitioned from mortal to immortal.</summary>
        public bool IsImmortal { get { return _data == null || _data.Phase == Phase.Immortal; } }
        /// <summary>Determines if this lifetime has permanently transitioned from mortal to dead.</summary>
        public bool IsDead { get { return _data != null && _data.Phase == Phase.Dead; } }

        /// <summary>
        /// Permanentaly transitions this lifetime to be either dead or immortal.
        /// No effect if already transitioned to the desired state.
        /// Invalid operation if already transitioned to another state.
        /// </summary>
        private void TransitionPermanently(bool isDead) {
            var newState = isDead ? Phase.Dead : Phase.Immortal;
            lock (_data) {
                // transition
                if (_data.Phase == newState) 
                    return;
                if (_data.Phase != Phase.Mortal)
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", _data.Phase, newState));
                _data.Phase = newState;

                // callback
                var callbacks = _data.Callbacks;
                _data.Callbacks = null;
                if (callbacks == null) return;
                foreach (var onDeathCallback in callbacks)
                    onDeathCallback();
            }
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        private Action Register(Action action) {
            // quick check for already finished
            if (!IsMortal) {
                action();
                return null;
            }

            lock (_data) {
                // can't run callbacks from limbo, so don't even keep the reference
                if (_data.Phase == Phase.MortalLimbo)
                    return null;

                // safe check for already finished
                if (_data.Phase != Phase.Mortal) {
                    action();
                    return null;
                }

                // add callback for when finished
                if (_data.Callbacks == null) _data.Callbacks = new List<Action>();
                _data.Callbacks.Add(action);
            }

            // return the 'cleanup' action that removes the registration
            var d = _data; // can't put struct fields in a closure, so copy to local
            var w = new WeakReference(action); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                lock (d) {
                    var a = (Action)w.Target;
                    if (a != null && d.Callbacks != null) {
                        d.Callbacks.Remove(a);
                    }
                }
            };
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is either dead or immortal.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead or immortal, the registration is cancelled.
        /// </summary>
        public void WhenNotMortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentException("Self-dependent registration.", "registrationLifetime");

            // when the registration lifetime is immortal, there's no need for complicated setup
            if (registrationLifetime.IsImmortal) {
                Register(action);
                return;
            }

            // *very carefully* setup the registrations so that they clean each other up
            Action cancel2 = null;
            var callCount = 0;
            Action cancel2OnSecondCall = () => {
                if (Interlocked.Increment(ref callCount) == 2 && cancel2 != null)
                    cancel2();
            };
            var cancel1 = Register(() => {
                action();
                cancel2OnSecondCall();
            });
            if (cancel1 == null) return;
            cancel2 = registrationLifetime.Register(() => {
                if (registrationLifetime.IsDead)
                    cancel1();
            });
            cancel2OnSecondCall();
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is dead.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead, the registration is cancelled.
        /// </summary>
        public void WhenDead(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentException("Self-dependent registration.", "registrationLifetime");

            // fast checks
            if (IsImmortal) return;
            if (registrationLifetime.IsDead) return;
            if (IsDead) {
                action();
                return;
            }

            var self = this;
            WhenNotMortal(
                () => { if (self.IsDead) action(); }, 
                registrationLifetime);
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is immortal.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes immortal, the registration is cancelled.
        /// </summary>
        public void WhenImmortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentException("Self-dependent registration.", "registrationLifetime");

            // fast checks
            if (IsDead) return;
            if (registrationLifetime.IsDead) return;
            if (IsImmortal) {
                action();
                return;
            }

            var self = this;
            WhenNotMortal(
                () => { if (self.IsImmortal) action(); },
                registrationLifetime);
        }

        /// <summary>
        /// Controls the fate of an exposed lifetime.
        /// The exposed lifetime dies or becomes immortal when the EndLifetime or GiveEternalLifetime methods are called, respectively.
        /// When a source is garbage collected without killing or immortalizing its exposed lifetime, the exposed lifetime becomes stuck in mortal limbo.
        /// </summary>
        [DebuggerDisplay("{ToString()}")]
        public sealed class Source {
            public Lifetime Lifetime { get; private set; }
            public Source() {
                this.Lifetime = new Lifetime(new Data { Phase = Phase.Mortal });
            }
            public void EndLifetime() {
                GC.SuppressFinalize(this);
                Lifetime.TransitionPermanently(true);
            }
            public void GiveEternalLifetime() {
                GC.SuppressFinalize(this);
                Lifetime.TransitionPermanently(false);
            }
            ~Source() {
                // well this isn't good... this source was collecteed without setting its lifetime
                // the lifetime may still be referenced, even though we aren't, but it will never be killed or immortalized
                // its callbacks will never run: it is in mortal limbo
                lock (Lifetime._data) {
                    Lifetime._data.Phase = Phase.MortalLimbo;
                    Lifetime._data.Callbacks = null;
                }
            }
            public override string ToString() {
                return Lifetime.ToString();
            }
        }

        public override string ToString() {
            return IsImmortal ? "Immortal"
                 : IsDead ? "Dead"
                 : _data.Phase == Phase.MortalLimbo ? "Limbo"
                 : "Alive";
        }
    }
}
