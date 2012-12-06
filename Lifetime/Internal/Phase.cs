namespace TwistedOak.Util {
    ///<summary>A state of life.</summary>
    internal enum Phase {
        /// <summary>The transient living state preceeding either death, immortality, or limbo.</summary>
        Mortal,
        /// <summary>The permanent non-living state.</summary>
        Dead,
        /// <summary>The permanent living state.</summary>
        Immortal,
        /// <summary>
        /// The permanent state stuck between being alive and not alive.
        /// Occurs when a lifetime's source is garbage collected, preventing it from ever being killed or immortalized.
        /// </summary>
        Limbo
    }
}