namespace TwistedOak.Util {
    internal enum Phase {
        /// <summary>The lifetime has not yet been killed or immortalized.</summary>
        Mortal,
        /// <summary>The lifetime has been permanently killed.</summary>
        Dead,
        /// <summary>The lifetime has been permanently immortalized.</summary>
        Immortal,
        /// <summary>
        /// The lifetime's source was garbage collected before the lifetime was killed or immortalized.
        /// Callbacks will not be run, because what they reference may have been finalized.
        /// The lifetime will never be killed or immortalized: it is stuck between the two states.
        /// </summary>
        MortalLimbo
    }
}