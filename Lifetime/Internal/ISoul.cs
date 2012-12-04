using System;

namespace TwistedOak.Util {
    internal interface ISoul {
        Phase Phase { get; }
        void WhenNotMortal(Action action, ISoul registrationLifetime);
        Action Register(Action action, bool isLimboSafe);
    }
}
