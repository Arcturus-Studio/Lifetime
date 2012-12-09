using System;

namespace Strilanc.Util.Soul {
    ///<summary>A permanently immortal soul.</summary>
    internal sealed class ImmortalSoul : ISoul {
        ///<summary>The single instance of the permanently dead soul.</summary>
        public static readonly ISoul Instance = new ImmortalSoul();
        private ImmortalSoul() { }
        public Phase Phase { get { return Phase.Immortal; } }
        public RegistrationRemover Register(Action action) {
            action();
            return Soul.EmptyRemover;
        }
    }
}
