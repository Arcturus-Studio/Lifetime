using System;

namespace Strilanc.Util.Soul {
    ///<summary>A permanently dead soul.</summary>
    internal sealed class DeadSoul : ISoul {
        ///<summary>The single instance of the permanently dead soul.</summary>
        public static readonly ISoul Instance = new DeadSoul();
        private DeadSoul() {}
        public Phase Phase { get { return Phase.Dead; } }
        public RegistrationRemover Register(Action action) {
            action();
            return Soul.EmptyRemover;
        }
    }
}
