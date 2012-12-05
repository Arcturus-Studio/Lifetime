using System;
using System.Threading;

namespace TwistedOak.Util {
    internal static class SoulUtils {
        public static readonly ISoul ImmortalSoul = new AnonymousSoul(
            () => Phase.Immortal,
            action => {
                action();
                return null;
            });
        public static readonly ISoul DeadSoul = new AnonymousSoul(
            () => Phase.Dead,
            action => {
                action();
                return null;
            });

        public static void WhenNotMortal(this ISoul self, Action action, ISoul registrationLifetime) {
            // avoid complicated setup when possible
            if (registrationLifetime.Phase == Phase.Dead) 
                return;
            if (self.Phase != Phase.Mortal) {
                action();
                return;
            }
            if (registrationLifetime.Phase == Phase.Immortal || registrationLifetime.Phase == Phase.Limbo) {
                self.Register(action);
                return;
            }

            // when the subscription lifetime is THIS lifetime, assume the callback invocation will beat the registration removal
            if (ReferenceEquals(self, registrationLifetime))
                registrationLifetime = ImmortalSoul;

            // *very carefully* setup the registrations so that they clean each other up
            Action cancelBack = null;
            var callCount = 0;
            Action cancelBackOnSecondCall = () => {
                if (Interlocked.Increment(ref callCount) == 2 && cancelBack != null)
                    cancelBack();
            };
            var cancel = self.Register(() => {
                action();
                cancelBackOnSecondCall();
            });
            if (cancel == null) return;
            cancelBack = registrationLifetime.Register(() => {
                if (registrationLifetime.Phase == Phase.Dead)
                    cancel();
            });
            cancelBackOnSecondCall();
        }
    }
}
