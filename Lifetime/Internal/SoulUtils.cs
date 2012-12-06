using System;
using System.Threading;

namespace TwistedOak.Util {
    ///<summary>Utility methods for working with souls</summary>
    internal static class SoulUtils {
        public static readonly RegistrationRemover EmptyRemover = () => { };

        private static ISoul MakePermanentSoul(Phase phase) {
            return new AnonymousSoul(
                () => phase,
                action => {
                    action();
                    return EmptyRemover;
                });
        }
        public static readonly ISoul ImmortalSoul = MakePermanentSoul(Phase.Immortal);
        public static readonly ISoul DeadSoul = MakePermanentSoul(Phase.Dead);
        public static readonly ISoul LimboSoul = MakePermanentSoul(Phase.Limbo);

        /// <summary>
        /// Returns a soul permanently stuck in the given phase.
        /// A permanently mortal soul is considered to be in limbo.
        /// </summary>
        public static ISoul AsPermanentSoul(this Phase phase) {
            if (phase == Phase.Dead) return DeadSoul;
            if (phase == Phase.Immortal) return ImmortalSoul;
            return LimboSoul;
        }
        /// <summary>
        /// Returns a lifetime permanently stuck in the given phase.
        /// A permanently mortal soul is considered to be in limbo.
        /// </summary>
        public static Lifetime AsPermanentLifetime(this Phase phase) {
            return new Lifetime(phase.AsPermanentSoul());
        }
        ///<summary>Returns a lifetime with a collapsing soul wrapping the given soul.</summary>
        public static Lifetime AsCollapsingLifetime(this ISoul soul) {
            return new Lifetime(new CollapsingSoul(soul));
        }

        ///<summary>Registers callbacks to each soul, ensuring everything is cleaned up properly upon completion.</summary>
        public static RegistrationRemover InterdependentRegister(ISoul soul1, Func<bool> tryComplete1, ISoul soul2, Func<bool> tryComplete2) {
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
        public static RegistrationRemover DependentRegister(this ISoul dependentSoul, Action action, ISoul necessarySoul) {
            if (dependentSoul == null) throw new ArgumentNullException("dependentSoul");
            if (action == null) throw new ArgumentNullException("action");
            if (necessarySoul == null) throw new ArgumentNullException("necessarySoul");

            // avoid complicated setup when possible
            if (necessarySoul.Phase == Phase.Dead)
                return EmptyRemover;
            if (dependentSoul.Phase != Phase.Mortal) {
                action();
                return EmptyRemover;
            }
            if (necessarySoul.Phase == Phase.Immortal || necessarySoul.Phase == Phase.Limbo) {
                dependentSoul.Register(action);
                return EmptyRemover;
            }

            // when the necessary soul is the same soul as the dependent soul, assume the callback invocation will beat the registration removal
            if (ReferenceEquals(dependentSoul, necessarySoul))
                necessarySoul = ImmortalSoul;

            return InterdependentRegister(
                dependentSoul,
                () => {
                    action();
                    return true;
                },
                necessarySoul,
                () => necessarySoul.Phase == Phase.Dead);
        }
    }
}
