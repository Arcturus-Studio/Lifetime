using System;

namespace TwistedOak.Util {
    ///<summary>Utility classes for manipulating lifetimes.</summary>
    public static class LifetimeUtilities {
        ///<summary>Returns a lifetime that dies when the given lifetime loses its mortality by either dying or becoming immortal.</summary>
        internal static Lifetime Mortality(this Lifetime lifetime) {
            return new Lifetime(new AnonymousSoul(
                () => {
                    var p = lifetime.Soul.Phase;
                    if (p == Phase.Immortal) return Phase.Dead;
                    return p;
                },
                lifetime.Soul.WhenNotMortal,
                lifetime.Soul.Register));
        }

        ///<summary>Returns a lifetime that dies when the given lifetime becomes immortal or becomes immortal when the given lifetime dies.</summary>
        internal static Lifetime Opposite(this Lifetime lifetime) {
            return new Lifetime(new AnonymousSoul(
                () => {
                    var p = lifetime.Soul.Phase;
                    if (p == Phase.Immortal) return Phase.Dead;
                    if (p == Phase.Dead) return Phase.Immortal;
                    return p;
                },
                lifetime.Soul.WhenNotMortal,
                lifetime.Soul.Register));
        }

        ///<summary>Returns a lifetime that dies when either of the given lifetimes dies or becomes immortal when both of the given lifetimes become immortal.</summary>
        //public static Lifetime Min(this Lifetime lifetime1, Lifetime lifetime2) {
        //    if (lifetime1.IsImmortal || lifetime2.IsDead) return lifetime2;
        //    if (lifetime2.IsImmortal || lifetime1.IsDead) return lifetime1;

        //    var s1 = lifetime1.Soul;
        //    var s2 = lifetime2.Soul;
        //    Func<Phase> g = () => {
        //        var p1 = s1.Phase;
        //        var p2 = s2.Phase;
        //        if (p1 == Phase.Dead || p2 == Phase.Dead) return Phase.Dead;
        //        if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
        //        if (p1 == Phase.MortalLimbo || p2 == Phase.MortalLimbo) return Phase.MortalLimbo;
        //        return Phase.Immortal;
        //    };
        //    return new Lifetime(new AnonymousSoul(
        //        g,
        //        (action, registrationLifetime) => {
        //            var subLife = new LifetimeSource();
        //            var s = subLife.Lifetime.Soul;
        //            registrationLifetime.WhenDead(subLife.EndLifetime, s);
        //            Action a = () => {
        //                var p = g();
        //                if (!p.IsMortal())
        //                action();
        //                subLife.EndLifetime();
        //            };

        //            // dead if either dies
        //            s1.WhenNotMortal(a, s);
        //            s2.WhenNotMortal(a, s);
        //            // immortal if both become immortal
        //            s1.WhenImmortal(() => s2.WhenImmortal(a, s), s);
        //        }));
        //}

        /////<summary>Returns a lifetime that becomes immortal when either of the given lifetimes becomes immortal or dies when both of the given lifetimes die.</summary>
        //public static Lifetime Max(this Lifetime lifetime1, Lifetime lifetime2) {
        //    return lifetime1.Opposite().Min(lifetime2.Opposite()).Opposite();
        //}
    }
}