using TwistedOak.Util.Soul;

namespace TwistedOak.Util {
    ///<summary>Utility classes for manipulating lifetimes.</summary>
    public static class LifetimeUtilities {
        ///<summary>Returns a lifetime that dies when either of the given lifetimes dies or becomes immortal when both of the given lifetimes become immortal.</summary>
        public static Lifetime Min(this Lifetime lifetime1, Lifetime lifetime2) {
            // try to avoid any wrapping at all
            if (lifetime1.IsImmortal) return lifetime2;
            if (lifetime2.IsImmortal) return lifetime1;
            if (Equals(lifetime1, lifetime2)) return lifetime1;

            return lifetime1.Soul.Combine(
                lifetime2.Soul,
                (p1, p2) => {
                    // dead < mortal < immortal
                    if (p1 == Phase.Dead || p2 == Phase.Dead) return Phase.Dead;
                    if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
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
                    // immortal > mortal > dead
                    if (p1 == Phase.Immortal) return Phase.Immortal;
                    if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
                    return Phase.Dead;
                }
            ).AsCollapsingLifetime();
        }

        /// <summary>
        /// Returns a new lifetime source that automatically kills its exposed lifetime if the given lifetime dies.
        /// Note: If the given lifetimes has died or will die then using ImmortalizeLifetime on the result will eventually cause an InvalidOperationException.
        /// </summary>
        public static LifetimeSource CreateDependentSource(this Lifetime lifetime) {
            var dependentResult = new LifetimeSource();
            lifetime.WhenDead(dependentResult.EndLifetime, dependentResult.Lifetime);
            return dependentResult;
        }
    }
}
