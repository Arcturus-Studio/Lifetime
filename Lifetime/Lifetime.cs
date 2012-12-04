using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TwistedOak.Element.Async;

namespace TwistedOak.Element.Util {
    [DebuggerDisplay("{ToString()}")]
    public struct Lifetime {
        private const int StateDead = 2;
        private const int StateMortal = 1;
        private const int StateImmortal = 0;

        public static readonly Lifetime Immortal = default(Lifetime);
        private readonly Data _data;
        private sealed class Data {
            public List<Action> OnDeathCallbacks;
            public int State; //0:immortal, 1:alive, 2:dead            
        }
        private Lifetime(Data data) {
            this._data = data;
        }

        public bool IsImmortal { get { return _data == null || _data.State == StateImmortal; } }
        public bool IsMortal { get { return _data != null && _data.State == StateMortal; } }
        public bool IsDead { get { return _data != null && _data.State == StateDead; } }

        ///<summary>Registers an action to perform when the lifetime ends, returning an action that cancels the registration when invoked.</summary>
        private Action MakeCancellableRegistration(Action action) {
            if (_data == null) {
                action();
                return null;
            }

            lock (_data) {
                // safe check for frozen states
                if (_data.State != StateMortal) {
                    action();
                    return null;
                }

                // register callback
                if (_data.OnDeathCallbacks == null) _data.OnDeathCallbacks = new List<Action>();
                _data.OnDeathCallbacks.Add(action);
            }

            var d = _data; // can't put struct fields in a closure, so copy to local
            return () => {
                lock (d) {
                    if (d.OnDeathCallbacks != null) {
                        d.OnDeathCallbacks.Remove(action);
                    }
                }
            };
        }

        ///<summary>Registers an action to perform when this lifetime ends, unless the given registration lifetime ends first.</summary>
        public void WhenDead(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentNullException("Self-dependent registration");

            // fast checks
            if (IsImmortal) return;
            if (registrationLifetime.IsDead) return;
            if (IsDead) {
                action();
                return;
            }

            var d = _data;
            WhenFixed(() => { if (d.State == StateDead) action(); }, registrationLifetime);
        }

        ///<summary>Registers an action to perform when this lifetime becomes immortal, unless the given registration lifetime ends first.</summary>
        public void WhenImmortal(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentNullException("Self-dependent registration");

            // fast checks
            if (IsDead) return;
            if (registrationLifetime.IsDead) return;
            if (IsImmortal) {
                action();
                return;
            }

            var d = _data;
            WhenFixed(() => { if (d.State == StateImmortal) action(); }, registrationLifetime);
        }

        ///<summary>Registers an action to perform when this lifetime becomes dead or immortal, unless the given registration lifetime ends first.</summary>
        public void WhenFixed(Action action, Lifetime registrationLifetime = default(Lifetime)) {
            if (action == null) throw new ArgumentNullException("action");
            if (_data != null && _data == registrationLifetime._data) throw new ArgumentNullException("Self-dependent registration");

            if (registrationLifetime.IsImmortal) {
                MakeCancellableRegistration(action);
                return;
            }

            // *very carefully* setup the registrations so that they clean each other up
            Action cancelReg = null;
            var cancelRegOnSecondPoke = new OnetimeLock();
            var cancelAction = MakeCancellableRegistration(() => {
                action();
                if (!cancelRegOnSecondPoke.TryAcquire()) cancelReg();
            });
            if (cancelAction == null) return;
            cancelReg = registrationLifetime.MakeCancellableRegistration(cancelAction);
            if (!cancelRegOnSecondPoke.TryAcquire()) cancelReg();
        }
        public static implicit operator CancellationToken(Lifetime lifetime) {
            if (lifetime.IsImmortal) return default(CancellationToken);
            var ct = new CancellationTokenSource();
            lifetime.WhenDead(ct.Cancel);
            return ct.Token;
        }

        private void Freeze(bool isDead) {
            lock (_data) {
                if (_data.State != StateMortal) return;

                _data.State = isDead ? StateDead : StateImmortal;

                var callbacks = _data.OnDeathCallbacks;
                _data.OnDeathCallbacks = null;
                if (callbacks == null) return;
                foreach (var onDeathCallback in callbacks)
                    onDeathCallback();
            }
        }

        [DebuggerDisplay("{ToString()}")]
        public sealed class Source {
            public Lifetime Lifetime { get; private set; }
            public Source() {
                this.Lifetime = new Lifetime(new Data { State = StateMortal });
            }
            public void EndLifetime() {
                Lifetime.Freeze(true);
            }
            public void GiveEternalLife() {
                Lifetime.Freeze(false);
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
