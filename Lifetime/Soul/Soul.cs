using System;
using System.Threading;

namespace Strilanc.Util.Soul {
    ///<summary>Utility methods for working with souls</summary>
    internal static class Soul {
        public static readonly RegistrationRemover EmptyRemover = () => { };

        /// <summary>
        /// Returns a soul permanently stuck in the given phase.
        /// A permanently mortal soul is considered to be immortal.
        /// </summary>
        public static ISoul AsPermanentSoul(this Phase phase) {
            return phase == Phase.Dead 
                 ? DeadSoul.Instance 
                 : ImmortalSoul.Instance;
        }
        /// <summary>
        /// Returns a lifetime permanently stuck in the given phase.
        /// A permanently mortal soul is considered to be immortal.
        /// </summary>
        public static Lifetime AsPermanentLifetime(this Phase phase) {
            return new Lifetime(phase.AsPermanentSoul());
        }
        ///<summary>Returns a lifetime for the given soul, collapsing it to a simpler soul when possible.</summary>
        public static Lifetime AsCollapsingLifetime(this ISoul soul) {
            // avoid any wrapping if possible
            var p = soul.Phase;
            if (p != Phase.Mortal) return p.AsPermanentLifetime();

            return new Lifetime(new CollapsingSoul(soul));
        }

        ///<summary>Registers callbacks to each soul, ensuring everything is cleaned up properly upon completion.</summary>
        public static RegistrationRemover InterdependentRegister(this ISoul soul1, Func<bool> tryComplete1, ISoul soul2, Func<bool> tryComplete2) {
            if (soul1 == null) throw new ArgumentNullException("soul1");
            if (tryComplete1 == null) throw new ArgumentNullException("tryComplete1");
            if (soul2 == null) throw new ArgumentNullException("soul2");
            if (tryComplete2 == null) throw new ArgumentNullException("tryComplete2");

            // forward declare the second registration canceller, so it can be referenced by the first registration
            RegistrationRemover cancelRegistration2 = null;
            var callCount = 0;
            Action skipOnceCancelRegistration2 = () => {
                if (Interlocked.Increment(ref callCount) == 2 && cancelRegistration2 != null)
                    cancelRegistration2();
            };

            // register the callbacks, linking their cancellation to each other
            var cancelRegistration1 = soul1.Register(() => { if (tryComplete1()) skipOnceCancelRegistration2(); });
            cancelRegistration2 = soul2.Register(() => { if (tryComplete2()) cancelRegistration1(); });
            
            // now that cancelRegistration2 has been initialized, we can allow it to be run
            skipOnceCancelRegistration2();

            // outside can force cleanup
            return () => {
                cancelRegistration1();
                cancelRegistration2();
            };
        }

        ///<summary>Registers a callback to the dependent soul that only occurs if the necessary soul doesn't die first, ensuring everything is cleaned up properly.</summary>
        public static RegistrationRemover DependentRegister(this ISoul soul, Action action, ISoul necessarySoul) {
            if (soul == null) throw new ArgumentNullException("soul");
            if (action == null) throw new ArgumentNullException("action");
            if (necessarySoul == null) throw new ArgumentNullException("necessarySoul");

            // when the necessary soul is the same soul as the dependent soul, assume the callback invocation will beat the registration removal
            if (ReferenceEquals(soul, necessarySoul))
                necessarySoul = ImmortalSoul.Instance;

            // avoid wrapping when possible
            if (necessarySoul.Phase == Phase.Dead)
                return EmptyRemover;
            if (soul.Phase != Phase.Mortal) {
                action();
                return EmptyRemover;
            }
            if (necessarySoul.Phase == Phase.Immortal)
                return soul.Register(action);

            return soul.InterdependentRegister(
                () => {
                    action();
                    return true;
                },
                necessarySoul,
                () => necessarySoul.Phase == Phase.Dead);
        }

        ///<summary>Combines two souls by using a custom function to combine their phases.</summary>
        public static ISoul Combine(this ISoul soul1, ISoul soul2, Func<Phase, Phase, Phase> phaseCombiner) {
            Func<Phase> getPhase = () => phaseCombiner(soul1.Phase, soul2.Phase);
            return new AnonymousSoul(
                getPhase,
                action => {
                    Func<bool> tryComplete = () => {
                        var hasPhase = getPhase() != Phase.Mortal;
                        if (hasPhase) action();
                        return hasPhase;
                    };
                    return soul1.InterdependentRegister(tryComplete, soul2, tryComplete);
                });
        }
    }
}
