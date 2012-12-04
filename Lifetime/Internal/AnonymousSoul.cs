using System;

namespace TwistedOak.Util {
    internal sealed class AnonymousSoul : ISoul {
        private readonly Func<Phase> _phase;
        private readonly Action<Action, ISoul> _whenNotMortal;
        private readonly Func<Action, bool, Action> _register;
        public AnonymousSoul(Func<Phase> phase, Action<Action, ISoul> whenNotMortal, Func<Action, bool, Action> register) {
            if (phase == null) throw new ArgumentNullException("phase");
            if (whenNotMortal == null) throw new ArgumentNullException("whenNotMortal");
            if (register == null) throw new ArgumentNullException("register");
            this._phase = phase;
            this._whenNotMortal = whenNotMortal;
            this._register = register;
        }

        public Phase Phase { get { return _phase(); } }
        public void WhenNotMortal(Action action, ISoul registrationLifetime) {
            _whenNotMortal(action, registrationLifetime);
        }
        public Action Register(Action action, bool isLimboSafe) {
            return _register(action, isLimboSafe);
        }
    }
}
