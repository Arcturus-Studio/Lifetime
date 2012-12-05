using System;

namespace TwistedOak.Util {
    internal sealed class AnonymousSoul : ISoul {
        private readonly Func<Phase> _phase;
        private readonly Func<Action, Action> _register;
        public AnonymousSoul(Func<Phase> phase, Func<Action, Action> register) {
            if (phase == null) throw new ArgumentNullException("phase");
            if (register == null) throw new ArgumentNullException("register");
            this._phase = phase;
            this._register = register;
        }

        public Phase Phase { get { return _phase(); } }
        public Action Register(Action action) {
            return _register(action);
        }
    }
}
