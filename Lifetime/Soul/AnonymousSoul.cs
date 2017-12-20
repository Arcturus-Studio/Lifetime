using System;
using System.Diagnostics;

namespace TwistedOak.Util.Soul {
    /// <summary>A soul implemented by delegates passed to its constructor.</summary>
    [DebuggerStepThrough]
    internal sealed class AnonymousSoul : ISoul {
        private readonly Func<Phase> _phase;
        private readonly Func<Action, RegistrationRemover> _register;
        public AnonymousSoul(Func<Phase> phase, Func<Action, RegistrationRemover> register) {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _register = register ?? throw new ArgumentNullException(nameof(register));
        }

        public Phase Phase => _phase();
        public RegistrationRemover Register(Action action) => _register(action);
    }
}
