using System;
using System.Threading;
using System.Linq;

namespace TwistedOak.Util {
    internal sealed class MortalSoul : ISoul {
        ///<summary>Callbacks to run when the lifetime is killed, immortalized, or enters limbo.</summary>
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
            DoublyLinkedNode<Action> callbacks;
            lock (this) {
                // transition
                if (Phase == newPhase)
                    return;
                if (Phase != Phase.Mortal)
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", Phase, newPhase));
                Phase = newPhase;

                // callbacks
                callbacks = _callbacks;
                _callbacks = null;
            }
            if (callbacks != null)
                foreach (var callback in callbacks.EnumerateOthers())
                    callback.Invoke();
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        public Action Register(Action action) {
            // quick check for already finished
            if (Phase != Phase.Mortal) {
                action();
                return null;
            }

            DoublyLinkedNode<Action> node;
            lock (this) {
                // safe check for already finished
                if (Phase != Phase.Mortal) {
                    action();
                    return null;
                }

                // add callback for when finished
                if (_callbacks == null) _callbacks = DoublyLinkedNode<Action>.CreateEmptyCycle();
                node = _callbacks.Prepend(action);
            }

            // return the 'cleanup' action that removes the registration
            var w = new WeakReference(node); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                var n = (DoublyLinkedNode<Action>)w.Target;
                if (n != null) n.Unlink();
            };
        }
    }
}
