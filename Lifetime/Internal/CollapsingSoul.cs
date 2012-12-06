using System;

namespace TwistedOak.Util {
    ///<summary>Delegates to an underlying soul that is replaced with one of the permanent souls once it is no longer mortal.</summary>
    internal sealed class CollapsingSoul : ISoul {
        private bool _collapsed;
        private ISoul _subSoul;
        
        public CollapsingSoul(ISoul subSoul) {
            this._subSoul = subSoul;
            
            // flatten multiple levels of wrapping
            var r = subSoul as CollapsingSoul;
            if (r != null) _subSoul = r._subSoul;
            
            // ensure collapse occurs once the sub soul becomes fixed
            Register(() => TryOptimize());
        }

        private Phase TryOptimize() {
            var phase = _subSoul.Phase;
            if (_collapsed) return phase; // already optimized
            if (phase == Phase.Mortal) return phase; // can't optimize yet

            // the following is idempotent, so it's fine if multiple writers race
            _collapsed = true;
            _subSoul = Phase.AsPermanentSoul();
            return phase;
        }
        public Phase Phase { get { return TryOptimize(); } }
        public RegistrationRemover Register(Action action) {
            return _subSoul.Register(action);
        }
    }
}
