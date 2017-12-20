using System;

namespace TwistedOak.Util.Soul {
    ///<summary>A permanently immortal soul.</summary>
    internal sealed class ImmortalSoul : ISoul {
        ///<summary>The single instance of the permanently dead soul.</summary>
        public static readonly ISoul Instance = new ImmortalSoul();
        private ImmortalSoul() { }
        public Phase Phase => Phase.Immortal;
        public RegistrationRemover Register(Action action) {
            action();
            return Soul.EmptyRemover;
        }
    }
}
