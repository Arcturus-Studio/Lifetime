using System;
using System.Threading;

namespace TwistedOak.Util {
    ///<summary>Utility classes for manipulating lifetimes.</summary>
    public static class LifetimeUtilities {
        ///<summary>Returns a lifetime that dies when the given lifetime loses its mortality by either dying or becoming immortal.</summary>
        internal static Lifetime Mortality(this Lifetime lifetime) {
            var s = lifetime.Soul;
            Func<Phase> mortality = () => {
                if (s.Phase == Phase.Immortal) return Phase.Dead;
                return s.Phase;
            };

            if (s.Phase != Phase.Mortal)
                return new Lifetime(SoulUtils.PermanentSoul(mortality()));

            return new Lifetime(new AnonymousSoul(mortality, s.Register));
        }

        ///<summary>Returns a lifetime that dies when the given lifetime becomes immortal or becomes immortal when the given lifetime dies.</summary>
        internal static Lifetime Opposite(this Lifetime lifetime) {
            var s = lifetime.Soul;
            Func<Phase> invert = () => {
                if (s.Phase == Phase.Immortal) return Phase.Dead;
                if (s.Phase == Phase.Dead) return Phase.Immortal;
                return s.Phase;
            };

            if (s.Phase != Phase.Mortal) 
                return new Lifetime(SoulUtils.PermanentSoul(invert()));
            
            return new Lifetime(new AnonymousSoul(invert, s.Register));
        }

        ///<summary>Returns a lifetime that dies when either of the given lifetimes dies or becomes immortal when both of the given lifetimes become immortal.</summary>
        public static Lifetime Min(this Lifetime lifetime1, Lifetime lifetime2) {
            var s1 = lifetime1.Soul;
            var s2 = lifetime2.Soul;
            Func<Phase> minPhase = () => {
                var p1 = s1.Phase;
                var p2 = s2.Phase;
                if (p1 == Phase.Dead || p2 == Phase.Dead) return Phase.Dead;
                if (p1 == Phase.Mortal || p2 == Phase.Mortal) return Phase.Mortal;
                if (p1 == Phase.Limbo || p2 == Phase.Limbo) return Phase.Limbo;
                return Phase.Immortal;
            };
            
            if (minPhase() != Phase.Mortal)
                return new Lifetime(SoulUtils.PermanentSoul(minPhase()));

            return new Lifetime(new AnonymousSoul(
                minPhase,
                action => {
                    Func<bool> tryComplete = () => {
                        var b = minPhase() != Phase.Mortal;
                        if (b) action();
                        return b;
                    };
                    return SoulUtils.InterdependentRegister(s1, tryComplete, s2, tryComplete);
                }));
        }

        ///<summary>Returns a lifetime that becomes immortal when either of the given lifetimes becomes immortal or dies when both of the given lifetimes die.</summary>
        public static Lifetime Max(this Lifetime lifetime1, Lifetime lifetime2) {
            return lifetime1.Opposite().Min(lifetime2.Opposite()).Opposite();
        }
    }
}