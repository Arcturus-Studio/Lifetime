using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

[TestClass]
public class LifetimeAndSourceTest {
    private static readonly Func<Action> InvalidCallbackMaker = () => {
        var r = new object();
        return () => {
            if (r == new object()) throw new Exception();
            throw new Exception();
        };
    };
    private static readonly Func<Action> ValidCallbackMaker = () => {
        var r = new object();
        return () => {
            if (r == new object()) {
                r = new object();
            }
        };
    };
    internal static Lifetime LimboedLifetime { get { using (var r = new LimboLife()) return r.Lifetime; } }
    internal sealed class DoomedLife : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        public Lifetime Lifetime { get { return this._source.Lifetime; } }
        public void Dispose() { _source.EndLifetime(); }
    }
    internal sealed class BlessedLife : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        public Lifetime Lifetime { get { return this._source.Lifetime; } }
        public void Dispose() { _source.ImmortalizeLifetime(); }
    }
    internal sealed class LimboLife : IDisposable {
        private LifetimeSource _source = new LifetimeSource();
        private readonly Lifetime _lifetime;
        public LimboLife() {
            this._lifetime = _source.Lifetime;
        }
        public Lifetime Lifetime { get { return _lifetime; } }
        public void Dispose() {
            _source = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [TestMethod]
    public void AsCancellationToken() {
        ((CancellationToken)Lifetime.Immortal).CanBeCanceled.AssertIsFalse();
        ((CancellationToken)Lifetime.Dead).IsCancellationRequested.AssertIsTrue();

        // cancelled on death
        var doomed = new LifetimeSource();
        CancellationToken dt = doomed.Lifetime;
        dt.CanBeCanceled.AssertIsTrue();
        dt.IsCancellationRequested.AssertIsFalse();
        doomed.EndLifetime();
        dt.IsCancellationRequested.AssertIsTrue();
        // already cancelled when already dead
        ((CancellationToken)doomed.Lifetime).IsCancellationRequested.AssertIsTrue();

        // hangs on immortal
        var blessed = new LifetimeSource();
        CancellationToken bt = blessed.Lifetime;
        bt.CanBeCanceled.AssertIsTrue();
        bt.IsCancellationRequested.AssertIsFalse();
        blessed.ImmortalizeLifetime();
        bt.IsCancellationRequested.AssertIsFalse();
        // knows can't be cancelled when already immortal
        ((CancellationToken)blessed.Lifetime).CanBeCanceled.AssertIsFalse();

        // hangs on limbo
        InvalidCallbackMaker.AssertNotCollectedAfter(action => {
            var r = new LifetimeSource();
            CancellationToken ct = r.Lifetime;
            ct.Register(action);
            return ct;
        });
    }

    [TestMethod]
    public void Equality() {
        // mortals are not congruent or equal
        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var a = new Func<Lifetime>[] {
            () => Lifetime.Immortal, 
            () => Lifetime.Dead,
            () => s1.Lifetime,
            () => s2.Lifetime
        };
        for (var i = 0; i < a.Length; i++) {
            for (var j = 0; j < a.Length; j++) {
                a[i]().IsCongruentTo(a[j]()).AssertEquals(i == j);
                a[i]().Equals(a[j]()).AssertEquals(i == j);
                if (i == j) a[i]().GetHashCode().AssertEquals(a[j]().GetHashCode());
            }
        }
        
        // but mortals become congruent when set to the same phase
        s1.EndLifetime();
        s2.EndLifetime();
        s1.Lifetime.IsCongruentTo(s2.Lifetime).AssertIsTrue();
    }

    [TestMethod]
    public void Status() {
        // status of constants
        Lifetime.Immortal.IsMortal.AssertIsFalse();
        Lifetime.Immortal.IsImmortal.AssertIsTrue();
        Lifetime.Immortal.IsDead.AssertIsFalse();
        Lifetime.Dead.IsMortal.AssertIsFalse();
        Lifetime.Dead.IsImmortal.AssertIsFalse();
        Lifetime.Dead.IsDead.AssertIsTrue();
        
        // state before transition
        var mortal = new LifetimeSource();
        mortal.Lifetime.IsMortal.AssertIsTrue();
        mortal.Lifetime.IsImmortal.AssertIsFalse();
        mortal.Lifetime.IsDead.AssertIsFalse();

        // transition to dead
        var doomed = new LifetimeSource();
        doomed.EndLifetime();
        doomed.Lifetime.IsMortal.AssertIsFalse();
        doomed.Lifetime.IsImmortal.AssertIsFalse();
        doomed.Lifetime.IsDead.AssertIsTrue();

        // transition to immortal
        var blessed = new LifetimeSource();
        blessed.ImmortalizeLifetime();
        blessed.Lifetime.IsMortal.AssertIsFalse();
        blessed.Lifetime.IsImmortal.AssertIsTrue();
        blessed.Lifetime.IsDead.AssertIsFalse();

        // transition to immortal via limbo
        var limbo = new LimboLife();
        limbo.Dispose();
        GC.Collect();
        limbo.Lifetime.IsMortal.AssertIsFalse();
        limbo.Lifetime.IsImmortal.AssertIsTrue();
        limbo.Lifetime.IsDead.AssertIsFalse();
    }

    [TestMethod]
    public void WhenSet() {
        // called when immortal?
        var blessed = new LifetimeSource();
        var preBlessedLife = blessed.Lifetime.WhenDeadTask();
        blessed.ImmortalizeLifetime();
        var bt = new[] {
            Lifetime.Immortal.WhenDeadTask(),
            blessed.Lifetime.WhenDeadTask(),
            preBlessedLife
        };
        Task.WhenAny(bt).AssertNotCompleted();

        // called when dead?
        var doomed = new LifetimeSource();
        var preDoomedLife = doomed.Lifetime.WhenDeadTask();
        doomed.EndLifetime();
        var dt = new[] {
            preDoomedLife, 
            doomed.Lifetime.WhenDeadTask(),
            Lifetime.Dead.WhenDeadTask()
        };
        Task.WhenAll(dt).AssertRanToCompletion();

        // never called from limbo
        var limboed = new LimboLife();
        var preLimboLife = limboed.Lifetime.WhenDeadTask();
        limboed.Dispose();
        Task.WhenAny(
            preLimboLife,
            limboed.Lifetime.WhenDeadTask()
        ).AssertNotCompleted();
    }

    [TestMethod]
    public void AllowsForGarbageCollection() {
        var mortal = new LifetimeSource();
        var lifes = new[] {
            Lifetime.Immortal,
            Lifetime.Dead,
            mortal.Lifetime
        };

        // callbacks are not kept when the lifetime is not mortal
        var f = ValidCallbackMaker;
        foreach (var life in lifes) {
            // pre-finished
            f.AssertCollectedAfter(e => Lifetime.Immortal.WhenDead(e, life));
            f.AssertCollectedAfter(e => Lifetime.Dead.WhenDead(e, life));
            f.AssertCollectedAfter(e => LimboedLifetime.WhenDead(e, life));
                
            // post-finished
            f.AssertCollectedAfter(e => { using (var limbod = new LimboLife()) limbod.Lifetime.WhenDead(e, life); });
            f.AssertCollectedAfter(e => { using (var blessed = new BlessedLife()) blessed.Lifetime.WhenDead(e, life); });
            f.AssertCollectedAfter(e => { using (var doomed = new DoomedLife()) doomed.Lifetime.WhenDead(e, life); });
        }

        // callbacks are not kept when the subscription lifetime is dead or dies
        f.AssertCollectedAfter(e => { using (var doomed = new DoomedLife()) mortal.Lifetime.WhenDead(e, doomed.Lifetime); });
        f.AssertCollectedAfter(e => mortal.Lifetime.WhenDead(e, Lifetime.Dead));

        // callbacks are kept when the lifetime is mortal and the subscription lifetime does not die
        f.AssertNotCollectedAfter(e => mortal.Lifetime.WhenDead(e, Lifetime.Immortal));
        f.AssertNotCollectedAfter(e => mortal.Lifetime.WhenDead(e, mortal.Lifetime));

        GC.KeepAlive(mortal);
    }

    [TestMethod]
    public void LifetimeDeathReentrancy() {
        var r = new LifetimeSource();
        var r2 = new LifetimeSource();
        r.Lifetime.WhenDead(r2.EndLifetime, r2.Lifetime);
        r.Lifetime.WhenDead(r.EndLifetime);
        r.EndLifetime();
    }

    [TestMethod]
    public void LifetimeDeathConcurrency() {
        var repeats = 20;
        foreach (var repeat in Enumerable.Range(0, repeats)) {
            LifetimeDeathConcurrency_Attempt(threadCount: 4, callbackCount: 10000);
        }
    }
    private static void LifetimeDeathConcurrency_Attempt(int threadCount, int callbackCount) {
        var n = 0;
        var source = new LifetimeSource();
        var threads =
            Enumerable.Range(0, threadCount)
            .Select(e => new Thread(() => {
                foreach (var i in Enumerable.Range(0, callbackCount)) {
                    source.Lifetime.WhenDead(() => Interlocked.Increment(ref n));
                }
                source.EndLifetime();
            }))
            .ToArray();
        
        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();
        
        n.AssertEquals(callbackCount * threadCount);
    }

    [TestMethod]
    public void LifetimeConditionalConcurrency() {
        var repeats = 20;
        foreach (var repeat in Enumerable.Range(0, repeats)) {
            LifetimeConditionalConcurrency_Attempt(threadCount: 4, callbackCount: 9000);
        }
    }
    private static void LifetimeConditionalConcurrency_Attempt(int threadCount, int callbackCount) {
        var n = 0;
        var sources = new[] {
            new LifetimeSource(),
            new LifetimeSource(),
            new LifetimeSource()
        };
        var threads =
            Enumerable.Range(0, threadCount)
            .Select(e => new Thread(() => {
                foreach (var i in Enumerable.Range(0, callbackCount)) {
                    var i1 = i % 3;
                    var i2 = (i/3)%2;
                    if (i1 <= i2) i2 += 1;
                    sources[i1].Lifetime.WhenDead(() => Interlocked.Increment(ref n), sources[i2].Lifetime);
                }
                sources[0].EndLifetime();
            }))
            .ToArray();

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();
        
        n.AssertEquals(callbackCount * threadCount * 2 / 6);
        sources[1].EndLifetime();
        n.AssertEquals(callbackCount * threadCount * 3 / 6);
        sources[2].EndLifetime();
        n.AssertEquals(callbackCount * threadCount * 3 / 6);
    }

    [TestMethod]
    public void LifetimeImmortalityConcurrency() {
        var repeats = 20;
        foreach (var repeat in Enumerable.Range(0, repeats)) {
            LifetimeImmortalityConcurrency_Attempt(threadCount: 4, callbackCount: 10000);
        }
    }
    private static void LifetimeImmortalityConcurrency_Attempt(int threadCount, int callbackCount) {
        var n = 0;
        var source = new LifetimeSource();
        var threads =
            Enumerable.Range(0, threadCount)
            .Select(e => new Thread(() => {
                foreach (var i in Enumerable.Range(0, callbackCount)) {
                    source.Lifetime.WhenDead(() => Interlocked.Increment(ref n));
                }
                source.ImmortalizeLifetime();
            }))
            .ToArray();

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        n.AssertEquals(0);
    }
}
