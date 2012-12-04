using System;
using System.Threading;

namespace TwistedOak.Util {
    internal sealed class MortalSoul : ISoul {
        /// <summary>
        /// Callbacks to run when the lifetime is killed, immortalized, or enters limbo because its source was finalized.
        /// Used for cleanup actions that have no externally visible effects other than allowing garbage collection.
        /// </summary>
        private event Action LimboSafeCallbacks;
        ///<summary>Callbacks to run when the lifetime is killed or immortalized.</summary>
        private event Action Callbacks;
        ///<summary>The current state of the lifetime.</summary>
        public Phase Phase { get; private set; }
        
        public MortalSoul() {
            Phase = Phase.Mortal;
        }

        /// <summary>
        /// Permanentaly transitions this lifetime to be either dead or immortal.
        /// No effect if already transitioned to the desired state.
        /// Invalid operation if already transitioned to another state.
        /// </summary>
        public void TransitionPermanently(Phase newPhase) {
            if (newPhase == Phase.Mortal) throw new ArgumentOutOfRangeException("newPhase");
            Action ev;
            lock (this) {
                // transition
                if (Phase == newPhase)
                    return;
                if (Phase != Phase.Mortal)
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", Phase, newPhase));
                Phase = newPhase;

                // callbacks
                ev = newPhase == Phase.MortalLimbo
                   ? LimboSafeCallbacks // can't run all callbacks when finalizing: targets may be in an invalid state due to finalization
                   : Callbacks + LimboSafeCallbacks;
                Callbacks = null;
                LimboSafeCallbacks = null;
            }
            if (ev != null) ev();
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        public Action Register(Action action, bool isLimboSafe) {
            // quick check for already finished
            if (!this.IsMortal()) {
                action();
                return null;
            }

            lock (this) {
                // check for limbo
                if (Phase == Phase.MortalLimbo) {
                    if (isLimboSafe) action();
                    return null;
                }

                // check for finished
                if (Phase != Phase.Mortal) {
                    action();
                    return null;
                }

                // add callback for when finished
                if (isLimboSafe) {
                    LimboSafeCallbacks += action;
                } else {
                    Callbacks += action;
                }
            }

            // return the 'cleanup' action that removes the registration
            var w = new WeakReference(action); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                var a = (Action)w.Target;
                if (a == null) return;
                if (isLimboSafe) {
                    LimboSafeCallbacks -= a;
                } else {
                    Callbacks -= a;
                }
            };
        }

        public void WhenNotMortal(Action action, ISoul registrationLifetime) {
            // avoid complicated setup when possible
            if (registrationLifetime.IsDead()) return;
            if (registrationLifetime.IsImmortal() || !this.IsMortal()) {
                Register(action, isLimboSafe: false);
                return;
            }

            // when the subscription lifetime is THIS lifetime (and both are mortal), just assume it dies afterwards
            if (!this.IsImmortal() && ReferenceEquals(this, registrationLifetime))
                registrationLifetime = ImmortalSoul.Instance;

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
                if (!registrationLifetime.IsDead()) return;
                if (cancel1 != null) cancel1();
                if (cancel2 != null) cancel2();
            }, isLimboSafe: true);
            cancelBackOnSecondCall();
        }
    }
}
