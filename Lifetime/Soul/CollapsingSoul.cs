using System;

namespace TwistedOak.Util.Soul {
    ///<summary>Delegates to an underlying soul that is replaced with one of the permanent souls once it is no longer mortal.</summary>
    internal sealed class CollapsingSoul : ISoul {
        private bool _collapsed;
        private ISoul _subSoul;
        
        public CollapsingSoul(ISoul subSoul) {
            this._subSoul = subSoul;
            
            // flatten multiple levels of wrapping
            var r = subSoul as CollapsingSoul;
            if (r != null) this._subSoul = r._subSoul;
            
            // ensure collapse occurs once the sub soul becomes fixed
            Register(() => TryOptimize());
        }

        private Phase TryOptimize() {
            var phase = this._subSoul.Phase;
            if (this._collapsed) return phase; // already optimized
            if (phase == Phase.Mortal) return phase; // can't optimize yet

            // the following is idempotent, so it's fine if multiple writers race
            this._collapsed = true;
            this._subSoul = this.Phase.AsPermanentSoul();
            return phase;
        }
        public Phase Phase { get { return TryOptimize(); } }
        public RegistrationRemover Register(Action action) {
            return this._subSoul.Register(action);
        }
    }
}
