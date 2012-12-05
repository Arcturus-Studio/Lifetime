using System;

namespace TwistedOak.Util {
    internal static class SoulUtils {
        public static readonly ISoul ImmortalSoul = new AnonymousSoul(
            () => Phase.Immortal,
            (action, registrationLifetime) => {
                if (!registrationLifetime.Phase.IsDead())
                    action();
            },
            (action, isLimboSafe) => {
                action();
                return null;
            });
        public static readonly ISoul DeadSoul = new AnonymousSoul(
            () => Phase.Dead,
            (action, registrationLifetime) => {
                if (!registrationLifetime.Phase.IsDead())
                    action();
            },
            (action, isLimboSafe) => {
                action();
                return null;
            });

        public static bool IsMortal(this Phase phase) {
            return phase == Phase.Mortal || phase == Phase.MortalLimbo;
        }
        public static bool IsDead(this Phase phase) {
            return phase == Phase.Dead;
        }
        public static bool IsImmortal(this Phase phase) {
            return phase == Phase.Immortal;
        }

        public static void WhenDead(this ISoul lifetime, Action action, ISoul registrationLifetime) {
            if (action == null) throw new ArgumentNullException("action");
            lifetime.WhenNotMortal(
                () => { if (lifetime.Phase.IsDead()) action(); },
                registrationLifetime);
        }

        public static void WhenImmortal(this ISoul lifetime, Action action, ISoul registrationLifetime) {
            if (action == null) throw new ArgumentNullException("action");
            lifetime.WhenNotMortal(
                () => { if (lifetime.Phase.IsImmortal()) action(); },
                registrationLifetime);
        }
    }
}