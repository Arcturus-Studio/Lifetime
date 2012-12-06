using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

[TestClass]
public class LifetimeAndSourceTest {
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
    public void Equality() {
        // mortals are not congruent or equal
        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var x = LimboedLifetime;
        var a = new Func<Lifetime>[] {
            () => Lifetime.Immortal, 
            () => Lifetime.Dead,
            () => x,
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
        Lifetime.Immortal.IsImmortal.AssertIsTrue();
        Lifetime.Immortal.IsDead.AssertIsFalse();
        Lifetime.Dead.IsImmortal.AssertIsFalse();
        Lifetime.Dead.IsDead.AssertIsTrue();

        // transition to dead
        var doomed = new LifetimeSource();
        doomed.Lifetime.IsImmortal.AssertIsFalse();
        doomed.Lifetime.IsDead.AssertIsFalse();
        doomed.EndLifetime();
        doomed.Lifetime.IsImmortal.AssertIsFalse();
        doomed.Lifetime.IsDead.AssertIsTrue();

        // transition to immortal
        var blessed = new LifetimeSource();
        blessed.Lifetime.IsImmortal.AssertIsFalse();
        blessed.Lifetime.IsDead.AssertIsFalse();
        blessed.ImmortalizeLifetime();
        blessed.Lifetime.IsImmortal.AssertIsTrue();
        blessed.Lifetime.IsDead.AssertIsFalse();

        // transition to limbo
        var limbod = new LimboLife();
        limbod.Lifetime.IsImmortal.AssertIsFalse();
        limbod.Lifetime.IsDead.AssertIsFalse();
        limbod.Dispose();
        GC.Collect();
        limbod.Lifetime.IsImmortal.AssertIsFalse();
        limbod.Lifetime.IsDead.AssertIsFalse();
    }

    [TestMethod]
    public void WhenSet() {
        WhenSetHelper(e => e.WhenImmortalTask(), false, true);
        WhenSetHelper(e => e.WhenDeadTask(), true, false);
        WhenSetHelper(e => e.WhenDeadOrImmortalTask(), true, true);
    }
    private static void WhenSetHelper(Func<Lifetime, Task> func, bool whenDead, bool whenImmortal) {
        // called when immortal?
        var blessed = new LifetimeSource();
        var preBlessedLife = func(blessed.Lifetime);
        blessed.ImmortalizeLifetime();
        var bt = new[] {
            func(Lifetime.Immortal),
            func(blessed.Lifetime),
            preBlessedLife
        };
        if (whenImmortal) {
            Task.WhenAll(bt).AssertRanToCompletion();
        } else {
            Task.WhenAny(bt).AssertNotCompleted();
        }

        // called when dead?
        var doomed = new LifetimeSource();
        var preDoomedLife = func(doomed.Lifetime);
        doomed.EndLifetime();
        var dt = new[] {
            preDoomedLife, 
            func(doomed.Lifetime),
            func(Lifetime.Dead)
        };
        if (whenDead) {
            Task.WhenAll(dt).AssertRanToCompletion();
        } else {
            Task.WhenAny(dt).AssertNotCompleted();
        }

        // never called from limbo
        var limboed = new LimboLife();
        var preLimboLife = limboed.Lifetime.WhenDeadTask();
        limboed.Dispose();
        Task.WhenAny(
            preLimboLife,
            func(limboed.Lifetime)
        ).AssertNotCompleted();
    }

    [TestMethod]
    public void AllowsForGarbageCollection() {
        var mortal = new LifetimeSource();
        var whens = new Action<Lifetime, Action, Lifetime>[] {
            (e, a, r) => e.WhenDead(a, r),
            (e, a, r) => e.WhenImmortal(a, r),
            (e, a, r) => e.WhenDeadOrImmortal(a, r)
        };
        var lifes = new[] {
            Lifetime.Immortal,
            Lifetime.Dead,
            mortal.Lifetime
        };

        var i = 0;
        Func<Action> f = () => {
            var r = i++;
            return () => i += r;
        };

        // callbacks are not kept when the lifetime is not mortal
        foreach (var when in whens) {
            foreach (var life in lifes) {
                // pre-finished
                f.AssertCollectedAfter(e => when(Lifetime.Immortal, e, life));
                f.AssertCollectedAfter(e => when(Lifetime.Dead, e, life));
                f.AssertCollectedAfter(e => when(LimboedLifetime, e, life));
                
                // post-finished
                f.AssertCollectedAfter(e => { using (var limbod = new LimboLife()) when(limbod.Lifetime, e, life); });
                f.AssertCollectedAfter(e => { using (var blessed = new BlessedLife()) when(blessed.Lifetime, e, life); });
                f.AssertCollectedAfter(e => { using (var doomed = new DoomedLife()) when(doomed.Lifetime, e, life); });
            }
        }

        // callbacks are not kept when the subscription lifetime is dead or dies
        foreach (var when in whens) {
            f.AssertCollectedAfter(e => { using (var doomed = new DoomedLife()) when(mortal.Lifetime, e, doomed.Lifetime); });
            f.AssertCollectedAfter(e => when(mortal.Lifetime, e, Lifetime.Dead));
        }

        // callbacks are kept when the lifetime is mortal and the subscription lifetime does not die
        foreach (var when in whens) {
            f.AssertNotCollectedAfter(e => when(mortal.Lifetime, e, Lifetime.Immortal));
            f.AssertNotCollectedAfter(e => when(mortal.Lifetime, e, mortal.Lifetime));
        }
    }
}
