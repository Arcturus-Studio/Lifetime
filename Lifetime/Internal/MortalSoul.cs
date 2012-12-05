using System;
using System.Threading;
using System.Linq;

namespace TwistedOak.Util {
    internal sealed class MortalSoul : ISoul {
        /// <summary>
        /// Callbacks to run when the lifetime is killed, immortalized, or enters limbo because its source was finalized.
        /// Used for cleanup actions that have no externally visible effects other than allowing garbage collection.
        /// </summary>
        private DoublyLinkedNode<Action> _limboSafeCallbacks;
        ///<summary>Callbacks to run when the lifetime is killed or immortalized.</summary>
        private DoublyLinkedNode<Action> _callbacks;
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
            DoublyLinkedNode<Action> ev1;
            DoublyLinkedNode<Action> ev2;
            lock (this) {
                // transition
                if (Phase == newPhase)
                    return;
                if (Phase != Phase.Mortal)
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", Phase, newPhase));
                Phase = newPhase;

                // callbacks
                ev1 = newPhase != Phase.MortalLimbo ? _callbacks : null; // can't run these when finalizing: targets may be in an invalid state due to finalization
                ev2 = _limboSafeCallbacks;
                _callbacks = null;
                _limboSafeCallbacks = null;
            }
            foreach (var callback in new[] {ev1, ev2}.Where(e => e != null).SelectMany(e => e.EnumerateOthers()))
                callback.Invoke();
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        public Action Register(Action action, bool isLimboSafe) {
            // quick check for already finished
            if (!Phase.IsMortal()) {
                action();
                return null;
            }

            DoublyLinkedNode<Action> node;
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
                    if (_limboSafeCallbacks == null) _limboSafeCallbacks = DoublyLinkedNode<Action>.CreateEmptyCycle();
                    node = _limboSafeCallbacks.Prepend(action);
                } else {
                    if (_callbacks == null) _callbacks =  DoublyLinkedNode<Action>.CreateEmptyCycle();
                    node = _callbacks.Prepend(action);
                }
            }

            // return the 'cleanup' action that removes the registration
            var w = new WeakReference(node); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                var n = (DoublyLinkedNode<Action>)w.Target;
                if (n != null) n.Unlink();
            };
        }

        public void WhenNotMortal(Action action, ISoul registrationLifetime) {
            // avoid complicated setup when possible
            if (registrationLifetime.Phase.IsDead()) return;
            if (registrationLifetime.Phase.IsImmortal() || !Phase.IsMortal()) {
                Register(action, isLimboSafe: false);
                return;
            }

            // when the subscription lifetime is THIS lifetime (and both are mortal), just assume it dies afterwards
            if (!Phase.IsImmortal() && ReferenceEquals(this, registrationLifetime))
                registrationLifetime = SoulUtils.ImmortalSoul;

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
                if (!registrationLifetime.Phase.IsDead()) return;
                if (cancel1 != null) cancel1();
                if (cancel2 != null) cancel2();
            }, isLimboSafe: true);
            cancelBackOnSecondCall();
        }
    }
}
