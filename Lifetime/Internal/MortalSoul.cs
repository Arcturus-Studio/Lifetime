using System;

namespace TwistedOak.Util {
    ///<summary>A soul that can be transitioned from mortal to some other phase.</summary>
    internal sealed class MortalSoul : ISoul {
        ///<summary>Callbacks to run when the lifetime is killed, immortalized, or enters limbo.</summary>
        private DoublyLinkedNode<Action> _callbacks;
        ///<summary>The current state of the lifetime.</summary>
        public Phase Phase { get; private set; }
        
        public MortalSoul() {
            Phase = Phase.Mortal;
        }

        public void TransitionPermanently(Phase newPhase) {
            if (!TryTransitionPermanently(newPhase))
                throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", Phase, newPhase));
        }
        /// <summary>
        /// Permanentaly transitions this lifetime to be either dead or immortal.
        /// No effect if already transitioned to the desired state.
        /// Invalid operation if already transitioned to another state.
        /// </summary>
        public bool TryTransitionPermanently(Phase newPhase) {
            if (newPhase == Phase.Mortal) throw new ArgumentOutOfRangeException("newPhase");
            DoublyLinkedNode<Action> callbacks;
            lock (this) {
                // transition
                if (Phase == newPhase)
                    return true;
                if (Phase != Phase.Mortal)
                    return false;
                Phase = newPhase;

                // callbacks
                callbacks = _callbacks;
                _callbacks = null;
            }
            if (callbacks != null)
                foreach (var callback in callbacks.EnumerateOthers())
                    callback.Invoke();
            return true;
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        public RegistrationRemover Register(Action action) {
            // hold a weak reference to the node, to ensure it can be collected after the this soul becomes non-mortal
            WeakReference weakNode;
            lock (this) {
                // safe check for already finished
                if (Phase != Phase.Mortal) {
                    action();
                    return SoulUtils.EmptyRemover;
                }

                // add callback for when finished
                if (_callbacks == null) _callbacks = DoublyLinkedNode<Action>.CreateEmptyCycle();
                weakNode = new WeakReference(_callbacks.Prepend(action));
            }

            // cleanup action that removes the registration
            return () => {
                var n = (DoublyLinkedNode<Action>)weakNode.Target;
                if (n != null) n.Unlink();
            };
        }
    }
}
