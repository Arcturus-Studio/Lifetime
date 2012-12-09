using System.Threading;

namespace Strilanc.Util {
    ///<summary>Creates lifetimes when requested, setting them when the next lifetime is requested.</summary>
    public sealed class LifetimeExchanger {
        private LifetimeSource _active = new LifetimeSource();
        
        ///<summary>Returns the current lifetime, that will be killed or immortalized before the next lifetime is created by the exchanger.</summary>
        public Lifetime ActiveLifetime { get { return _active.Lifetime; } }

        ///<summary>Returns a newly created mortal lifetime after killing the previously created lifetime (if any).</summary>
        public Lifetime StartNextAndEndPreviousLifetime() {
            var next = new LifetimeSource();
            var prev = Interlocked.Exchange(ref _active, next);
            if (prev != null) prev.EndLifetime();
            return next.Lifetime;
        }

        ///<summary>Returns a newly created mortal lifetime after giving eternal life to the previously created lifetime (if any).</summary>
        public Lifetime StartNextAndImmortalizePreviousLifetime() {
            var next = new LifetimeSource();
            var prev = Interlocked.Exchange(ref _active, next);
            if (prev != null) prev.ImmortalizeLifetime();
            return next.Lifetime;
        }
    }
}
