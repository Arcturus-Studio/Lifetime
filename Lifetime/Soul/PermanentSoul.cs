using System;

namespace TwistedOak.Util.Soul {
    ///<summary>A permanently set soul.</summary>
    internal sealed class PermanentSoul : ISoul {
        ///<summary>A permanently immortal soul.</summary>
        public static readonly ISoul Immortal = new PermanentSoul(Phase.Immortal);
        ///<summary>A permanently dead soul.</summary>
        public static readonly ISoul Dead = new PermanentSoul(Phase.Dead);
        
        public Phase Phase { get; private set; }
        private PermanentSoul(Phase phase) {
            this.Phase = phase;
        }
        public RegistrationRemover Register(Action action) {
            action();
            return Soul.EmptyRemover;
        }
    }
}
