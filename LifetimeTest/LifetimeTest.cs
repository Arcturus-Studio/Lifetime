using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

internal static class LifetimeTestUtil {
    public static Task WhenDeadTask(this Lifetime lifetime) {
        var t = new TaskCompletionSource<bool>();
        lifetime.WhenDead(() => t.SetResult(true));
        return t.Task;
    }
    public static Task WhenImmortalTask(this Lifetime lifetime) {
        var t = new TaskCompletionSource<bool>();
        lifetime.WhenImmortal(() => t.SetResult(true));
        return t.Task;
    }
    public static Task WhenNotMortalTask(this Lifetime lifetime) {
        var t = new TaskCompletionSource<bool>();
        lifetime.WhenDeadOrImmortal(() => t.SetResult(true));
        return t.Task;
    }
    public static void AssertCollectedAfter<T>(this Func<T> func, Action<T> action) where T : class {
        var value = func();
        var r = new WeakReference<T>(value);
        action(value);
        value = null;
        GC.Collect();
        r.TryGetTarget(out value).AssertIsFalse();
    }
    public static void AssertNotCollectedAfter<T>(this Func<T> func, Action<T> action) where T : class {
        var value = func();
        var r = new WeakReference<T>(value);
        action(value);
        value = null;
        GC.Collect();
        r.TryGetTarget(out value).AssertIsTrue();
    }
}
[TestClass]
public class LifetimeTest {
    internal static Lifetime LimboedLifetime { get { using (var r = new LimboLife()) return r.Lifetime; } }
    internal sealed class DoomedLife : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        public Lifetime Lifetime { get { return this._source.Lifetime; } }
        public void Dispose() { _source.EndLifetime(); }
    }
    internal sealed class BlessedLife : IDisposable {
        private readonly LifetimeSource _source = new LifetimeSource();
        public Lifetime Lifetime { get { return this._source.Lifetime; } }
        public void Dispose() { _source.GiveEternalLifetime(); }
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
    public void LifetimeIsProperties() {
        Lifetime.Immortal.IsImmortal.AssertIsTrue();
        Lifetime.Immortal.IsDead.AssertIsFalse();
        Lifetime.Dead.IsImmortal.AssertIsFalse();
        Lifetime.Dead.IsDead.AssertIsTrue();

        var doomed = new DoomedLife();
        using (doomed) {
            doomed.Lifetime.IsImmortal.AssertIsFalse();
            doomed.Lifetime.IsDead.AssertIsFalse();
        }
        doomed.Lifetime.IsImmortal.AssertIsFalse();
        doomed.Lifetime.IsDead.AssertIsTrue();

        var blessed = new BlessedLife();
        using (blessed) {
            blessed.Lifetime.IsImmortal.AssertIsFalse();
            blessed.Lifetime.IsDead.AssertIsFalse();
        }
        blessed.Lifetime.IsImmortal.AssertIsTrue();
        blessed.Lifetime.IsDead.AssertIsFalse();

        var limbod = new LimboLife();
        using (limbod) {
            limbod.Lifetime.IsImmortal.AssertIsFalse();
            limbod.Lifetime.IsDead.AssertIsFalse();
        }
        limbod.Lifetime.IsImmortal.AssertIsFalse();
        limbod.Lifetime.IsDead.AssertIsFalse();
    }

    private static void AssertWhenX(Func<Lifetime, Task> func, bool whenDead, bool whenImmortal) {
        // called when immortal?
        var blessed = new BlessedLife();
        var blessedLife = func(blessed.Lifetime);
        blessed.Dispose();
        var bt = new[] {
            func(Lifetime.Immortal),
            func(blessed.Lifetime),
            blessedLife
        };
        if (whenImmortal) {
            Task.WhenAll(bt).AssertRanToCompletion();
        } else {
            Task.WhenAny(bt).AssertNotCompleted();
        }

        // called when dead?
        var doomed = new DoomedLife();
        var doomedLife = func(doomed.Lifetime);
        doomed.Dispose();
        var dt = new[] {
            doomedLife, 
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
        var limboLife = limboed.Lifetime.WhenDeadTask();
        limboed.Dispose();
        Task.WhenAny(
            limboLife,
            func(limboed.Lifetime)
        ).AssertNotCompleted();
    }
    [TestMethod]
    public void WhenSet() {
        AssertWhenX(e => e.WhenImmortalTask(), false, true);
        AssertWhenX(e => e.WhenDeadTask(), true, false);
        AssertWhenX(e => e.WhenNotMortalTask(), true, true);
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
