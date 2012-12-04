using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TwistedOak.Util {
    /// <summary>
    /// Runs callbacks when transitioning permanently from mortal to either dead or immortal.
    /// The default lifetime is immortal.
    /// Lifetimes whose source is garbage collected are neither dead nor immortal: they are in mortal limbo and will discard all callbacks.
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
            /// <summary>
            /// Callbacks to run when the lifetime is killed, immortalized, or enters limbo because its source was finalized.
            /// Used for cleanup actions that have no externally visible effects other than allowing garbage collection.
            /// </summary>
            public List<Action> LimboSafeCallbacks;
            ///<summary>Callbacks to run when the lifetime is killed or immortalized.</summary>
            public List<Action> Callbacks;
            ///<summary>The current state of the lifetime.</summary>
            public Phase Phase;
            ///<summary>Nulls the referenced field, running any actions that were referenced by it.</summary>
            public static void RunAndClearCallbacks(ref List<Action> callbacksField) {
                var callbacks = callbacksField;
                callbacksField = null;
                if (callbacks == null) return;
                foreach (var callback in callbacks)
                    callback();
            }
        }

        /// <summary>
        /// The default lifetime.
        /// A lifetime that has already permanently transitioned from mortal to immortal.
        /// </summary>
        public static readonly Lifetime Immortal = default(Lifetime);
        /// <summary>
        /// NOT the default lifetime.
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

                // callbacks
                Data.RunAndClearCallbacks(ref _data.Callbacks);
                Data.RunAndClearCallbacks(ref _data.LimboSafeCallbacks);
            }
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        private Action Register(Action action, bool isLimboSafe) {
            // quick check for already finished
            if (!IsMortal) {
                action();
                return null;
            }

            lock (_data) {
                // can't run callbacks from limbo, so don't even keep the reference
                if (_data.Phase == Phase.MortalLimbo) {
                    if (isLimboSafe) action();
                    return null;
                }

                // safe check for already finished
                if (_data.Phase != Phase.Mortal) {
                    action();
                    return null;
                }

                // add callback for when finished
                if (isLimboSafe) {
                    if (_data.LimboSafeCallbacks == null) _data.LimboSafeCallbacks = new List<Action>();
                    _data.LimboSafeCallbacks.Add(action);
                } else {
                    if (_data.Callbacks == null) _data.Callbacks = new List<Action>();
                    _data.Callbacks.Add(action);
                }
            }

            // return the 'cleanup' action that removes the registration
            var d = _data; // can't put struct fields in a closure, so copy to local
            var w = new WeakReference(action); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                lock (d) {
                    var a = (Action)w.Target;
                    if (a == null) return;
                    if (isLimboSafe) {
                        if (d.LimboSafeCallbacks != null) {
                            d.LimboSafeCallbacks.Remove(a);
                        }
                    } else {
                        if (d.Callbacks != null) {
                            d.Callbacks.Remove(a);
                        }
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

            // avoid complicated setup when possible
            if (registrationLifetime.IsDead) return;
            if (registrationLifetime.IsImmortal || !IsMortal) {
                Register(action, isLimboSafe: false);
                return;
            }

            // when the subscription lifetime is THIS lifetime (and both are mortal), just assume it dies afterwards
            if (!IsImmortal && _data == registrationLifetime._data)
                registrationLifetime = Immortal;

            // *very carefully* setup the registrations so that they clean each other up
            Action cancelBack = null;
            var callCount = 0;
            Action cancelBackOnSecondCall = () => {
                if (Interlocked.Increment(ref callCount) == 2 && cancelBack != null)
                    cancelBack();
            };
            var cancel1 = Register(action, isLimboSafe: false);
            var cancel2 = Register(cancelBackOnSecondCall, isLimboSafe: true);
            cancelBack = registrationLifetime.Register(() => {
                if (!registrationLifetime.IsDead) return;
                if (cancel1 != null) cancel1();
                if (cancel2 != null) cancel2();
            }, isLimboSafe: true);
            cancelBackOnSecondCall();
        }

        /// <summary>
        /// Registers an action to perform when this lifetime is dead.
        /// If a registration lifetime is given and becomes dead before this lifetime becomes dead, the registration is cancelled.
        /// </summary>
        public void WhenDead(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");

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
            /// <summary>The lifetime exposed and managed by the lifetime source.</summary>
            public Lifetime Lifetime { get; private set; }
            /// <summary>Constructs a new lifetime source with an initially mortal exposed lifetime.</summary>
            public Source() {
                this.Lifetime = new Lifetime(new Data { Phase = Phase.Mortal });
            }
            /// <summary>
            /// Permanently transitions the source's exposed lifetime from mortal to dead.
            /// No effect when the exposed lifetime is already dead.
            /// Invalid operation when the exposed lifetime is immortal.
            /// </summary>
            public void EndLifetime() {
                GC.SuppressFinalize(this);
                Lifetime.TransitionPermanently(true);
            }
            /// <summary>
            /// Permanently transitions the source's exposed lifetime from mortal to immortal.
            /// No effect when the exposed lifetime is already immortal.
            /// Invalid operation when the exposed lifetime is dead.
            /// </summary>
            public void GiveEternalLifetime() {
                GC.SuppressFinalize(this);
                Lifetime.TransitionPermanently(false);
            }
            ~Source() {
                // well this isn't good... this source was collected without setting its lifetime
                // the lifetime may still be referenced, even though we aren't, but it will never be killed or immortalized
                // its callbacks will never run: it is in mortal limbo
                lock (Lifetime._data) {
                    Lifetime._data.Phase = Phase.MortalLimbo;
                    Lifetime._data.Callbacks = null; // can't run these callbacks: their targets may be in an invalid state due to finalization
                    Data.RunAndClearCallbacks(ref Lifetime._data.LimboSafeCallbacks); // cleanup callbacks are allowed, though
                }
            }
            ///<summary>Returns a text representation of the lifetime source's current state.</summary>
            public override string ToString() {
                return Lifetime.ToString();
            }
        }

        ///<summary>Returns a text representation of the lifetime's current state.</summary>
        public override string ToString() {
            return IsImmortal ? "Immortal"
                 : IsDead ? "Dead"
                 : _data.Phase == Phase.MortalLimbo ? "Alive (Limbo)"
                 : "Alive";
        }
    }
}
