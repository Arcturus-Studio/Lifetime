using TwistedOak.Util.Soul;

namespace TwistedOak.Util {
    ///<summary>Utility classes for manipulating lifetimes.</summary>
    public static class LifetimeUtilities {
        ///<summary>Returns a lifetime that dies when the given lifetime loses its mortality by either dying or becoming immortal.</summary>
        public static Lifetime Mortality(this Lifetime lifetime) {
            var s = lifetime.Soul;
            return new AnonymousSoul(
                () => {
                    if (s.Phase == Phase.Immortal) return Phase.Dead;
                    return s.Phase;
                },
                s.Register
            ).AsCollapsingLifetime();
        }

        ///<summary>Returns a lifetime that dies when the given lifetime becomes immortal or becomes immortal when the given lifetime dies.</summary>
        public static Lifetime Opposite(this Lifetime lifetime) {
            var s = lifetime.Soul;
            return new AnonymousSoul(
                () => {
                    if (s.Phase == Phase.Immortal) return Phase.Dead;
                    if (s.Phase == Phase.Dead) return Phase.Immortal;
                    return s.Phase;
                }, 
                s.Register
            ).AsCollapsingLifetime();
        }

        ///<summary>Returns a lifetime that dies when either of the given lifetimes dies or becomes immortal when both of the given lifetimes become immortal.</summary>
        public static Lifetime Min(this Lifetime lifetime1, Lifetime lifetime2) {
            // try to avoid any wrapping at all
            if (lifetime1.IsImmortal) return lifetime2;
            if (lifetime2.IsImmortal) return lifetime1;
            if (Equals(lifetime1, lifetime2)) return lifetime1;

            return lifetime1.Soul.Combine(
                lifetime2.Soul,
                (p1, p2) => {
                    // dead < mortal < limbo < immortal
                    if (p1 == Phase.Dead || p2 == Phase.Dead) return Phase.Dead;
                    if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
                    if (p1 == Phase.Limbo || p2 == Phase.Limbo) return Phase.Limbo;
                    return Phase.Immortal;
                }
            ).AsCollapsingLifetime();
        }

        ///<summary>Returns a lifetime that becomes immortal when either of the given lifetimes becomes immortal or dies when both of the given lifetimes die.</summary>
        public static Lifetime Max(this Lifetime lifetime1, Lifetime lifetime2) {
            // try to avoid any wrapping at all
            if (lifetime1.IsDead) return lifetime2;
            if (lifetime2.IsDead) return lifetime1;
            if (Equals(lifetime1, lifetime2)) return lifetime1;

            return lifetime1.Soul.Combine(
                lifetime2.Soul,
                (p1, p2) => {
                    // immortal > mortal > limbo > dead
                    if (p1 == Phase.Immortal || p2 == Phase.Immortal) return Phase.Immortal;
                    if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
                    if (p1 == Phase.Limbo || p2 == Phase.Limbo) return Phase.Limbo;
                    return Phase.Dead;
                }
            ).AsCollapsingLifetime();
        }
    }
}
