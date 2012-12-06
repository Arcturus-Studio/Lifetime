using System;

namespace TwistedOak.Util.Soul {
    internal delegate void RegistrationRemover();

    ///<summary>The internal representation of a lifetime.</summary>
    internal interface ISoul {
        ///<summary>The soul's state, either transiently mortal or permanently dead/immortal.</summary>
        Phase Phase { get; }
        /// <summary>
        /// Adds a callback to be run when the soul is not mortal.
        /// Returns a delegate that, if run before the soul is non-mortal, cancels the registration.
        /// </summary>
        RegistrationRemover Register(Action action);
    }
}
