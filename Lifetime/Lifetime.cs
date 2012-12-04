using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TwistedOak.Util {
    [DebuggerDisplay("{ToString()}")]
    public struct Lifetime {
        public static readonly Lifetime Immortal = default(Lifetime);
        private enum LifeState {
            Immortal = 0,
            Mortal = 1,
            Dead = 2
        }

        private readonly Data _data;
        private sealed class Data {
            public List<Action> OnDeathCallbacks;
            public LifeState State;
        }
        private Lifetime(Data data) {
            this._data = data;
        }

        public bool IsImmortal { get { return _data == null || _data.State == LifeState.Immortal; } }
        public bool IsMortal { get { return _data != null && _data.State == LifeState.Mortal; } }
        public bool IsDead { get { return _data != null && _data.State == LifeState.Dead; } }

        private void Set(bool isDead) {
            var newState = isDead ? LifeState.Dead : LifeState.Immortal;
            lock (_data) {
                // transition
                if (_data.State == newState) return;
                if (_data.State != LifeState.Mortal) {
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", _data.State, newState));
                }
                _data.State = newState;

                // callback
                var callbacks = _data.OnDeathCallbacks;
                _data.OnDeathCallbacks = null;
                if (callbacks == null) return;
                foreach (var onDeathCallback in callbacks)
                    onDeathCallback();
            }
        }

        /// <summary>
        /// Registers a given action to perform when this lifetime becomes immortal or dead.
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
                // safe check for already finished
                if (_data.State != LifeState.Mortal) {
                    action();
                    return null;
                }

                // add callback for when finished
                if (_data.OnDeathCallbacks == null) _data.OnDeathCallbacks = new List<Action>();
                _data.OnDeathCallbacks.Add(action);
            }

            // return the 'cleanup' action that removes the registration
            var d = _data; // can't put struct fields in a closure, so copy to local
            var w = new WeakReference(action); // prevent user holding onto the returned action from extending the lifetime of closed over objects
            return () => {
                lock (d) {
                    var a = (Action)w.Target;
                    if (a != null && d.OnDeathCallbacks != null) {
                        d.OnDeathCallbacks.Remove(a);
                    }
                }
            };
        }

        /// <summary>
        /// Registers an action to perform when this lifetime ends, unless the given registration lifetime ends first.</summary>
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

            var d = _data;
            WhenNotMortal(() => { if (d.State == LifeState.Dead) action(); }, registrationLifetime);
        }

        ///<summary>Registers an action to perform when this lifetime becomes immortal, unless the given registration lifetime ends first.</summary>
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

            var d = _data;
            WhenNotMortal(() => { if (d.State == LifeState.Immortal) action(); }, registrationLifetime);
        }

        ///<summary>Registers an action to perform when this lifetime becomes dead or immortal, unless the given registration lifetime ends first.</summary>
        public void WhenNotMortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentException("Self-dependent registration.", "registrationLifetime");

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

        [DebuggerDisplay("{ToString()}")]
        public sealed class Source {
            public Lifetime Lifetime { get; private set; }
            public Source() {
                this.Lifetime = new Lifetime(new Data { State = LifeState.Mortal });
            }
            public void EndLifetime() {
                Lifetime.Set(true);
            }
            public void GiveEternalLife() {
                Lifetime.Set(false);
            }
            public override string ToString() {
                return Lifetime.ToString();
            }
        }

        public override string ToString() {
            return IsImmortal ? "Immortal"
                 : IsDead ? "Dead"
                 : "Alive";
        }
    }
}
