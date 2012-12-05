using System;

namespace TwistedOak.Util {
    internal interface ISoul {
        Phase Phase { get; }
        Action Register(Action action);
    }
}
