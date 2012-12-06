using System;

namespace TwistedOak.Util.Soul {
    ///<summary>A soul implemented by delegates passed to its constructor.</summary>
    internal sealed class AnonymousSoul : ISoul {
        private readonly Func<Phase> _phase;
        private readonly Func<Action, RegistrationRemover> _register;
        public AnonymousSoul(Func<Phase> phase, Func<Action, RegistrationRemover> register) {
            if (phase == null) throw new ArgumentNullException("phase");
            if (register == null) throw new ArgumentNullException("register");
            this._phase = phase;
            this._register = register;
        }

        public Phase Phase { get { return this._phase(); } }
        public RegistrationRemover Register(Action action) {
            return this._register(action);
        }
    }
}
