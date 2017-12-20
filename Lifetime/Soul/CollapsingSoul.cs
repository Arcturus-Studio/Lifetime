using System;

namespace TwistedOak.Util.Soul {
    /// <summary>
    /// Delegates to an underlying soul that is replaced with one of the permanent
    /// souls once it is no longer mortal.
    /// </summary>
    internal sealed class CollapsingSoul : ISoul {
        private bool _collapsed;
        private ISoul _subSoul;
        
        public CollapsingSoul(ISoul subSoul) {
            _subSoul = subSoul;

            // flatten multiple levels of wrapping
            if(subSoul is CollapsingSoul cS) _subSoul = cS._subSoul;

            // ensure collapse occurs once the sub soul becomes fixed
            Register(() => tryOptimize());
        }

        private Phase tryOptimize() {
            var phase = _subSoul.Phase;
            if (_collapsed) return phase; // already optimized
            if (phase == Phase.Mortal) return phase; // can't optimize yet

            // the following is idempotent, so it's fine if multiple writers race
            _collapsed = true;
            _subSoul = Phase.AsPermanentSoul();
            return phase;
        }

        public Phase Phase => tryOptimize();
        public RegistrationRemover Register(Action action) => _subSoul.Register(action);
    }
}
