using System;

namespace TwistedOak.Util.Soul {
    ///<summary>A permanently set soul.</summary>
    internal sealed class PermanentSoul : ISoul {
        public static readonly ISoul Immortal = new PermanentSoul(Phase.Immortal);
        public static readonly ISoul Dead = new PermanentSoul(Phase.Dead);
        public static readonly ISoul Limbo = new PermanentSoul(Phase.Limbo);
        
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
