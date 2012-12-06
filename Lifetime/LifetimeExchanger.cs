using System.Threading;

namespace TwistedOak.Util {
    ///<summary>Creates lifetimes when requested, ending them when the next lifetime is requested.</summary>
    public sealed class LifetimeExchanger {
        private LifetimeSource _active;
        
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

        ///<summary>Kills the previously created lifetime (if any).</summary>
        public void EndPreviousLifetime() {
            var prev = Interlocked.Exchange(ref _active, null);
            if (prev != null) prev.EndLifetime();
        }
        ///<summary>Gives eternal life to the previously created lifetime (if any).</summary>
        public void ImmortalizePreviousLifetime() {
            var prev = Interlocked.Exchange(ref _active, null);
            if (prev != null) prev.ImmortalizeLifetime();
        }
    }
}
