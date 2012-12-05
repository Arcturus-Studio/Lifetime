using System;

namespace TwistedOak.Util {
    internal delegate void RegistrationRemover();
    internal interface ISoul {
        Phase Phase { get; }
        RegistrationRemover Register(Action action);
    }
}
