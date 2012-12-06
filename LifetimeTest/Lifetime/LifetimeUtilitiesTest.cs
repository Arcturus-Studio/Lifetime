using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

[TestClass]
public class LifetimeUtilitiesTest {
    private static readonly Func<Action> InvalidCallbackMaker = () => {
        var r = new object();
        return () => {
            if (r == new object()) throw new Exception();
            throw new Exception();
        };
    };
    
    [TestMethod]
    public void Mortality() {
        Lifetime.Immortal.Mortality().IsDead.AssertIsTrue();
        Lifetime.Dead.Mortality().IsDead.AssertIsTrue();

        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var m1 = s1.Lifetime.Mortality();
        var m2 = s2.Lifetime.Mortality();
        var t1 = m1.WhenDeadOrImmortalTask();
        var t2 = m2.WhenDeadOrImmortalTask();
        
        (m1.IsDead || m1.IsImmortal).AssertIsFalse();
        (m2.IsDead || m2.IsImmortal).AssertIsFalse();
        Task.WhenAny(t1, t2).AssertNotCompleted();
        
        s1.EndLifetime();
        s2.ImmortalizeLifetime();

        m1.IsDead.AssertIsTrue();
        m2.IsDead.AssertIsTrue();
        Task.WhenAll(t1, t2).AssertRanToCompletion();

        // limbo allows collection
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r = new LifetimeSource();
            var life = r.Lifetime.Mortality();
            life.WhenDeadOrImmortal(a);
            return life;
        });
    }

    [TestMethod]
    public void Opposite() {
        Lifetime.Immortal.Opposite().IsDead.AssertIsTrue();
        Lifetime.Dead.Opposite().IsImmortal.AssertIsTrue();

        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var m1 = s1.Lifetime.Opposite();
        var m2 = s2.Lifetime.Opposite();
        var t1 = m1.WhenDeadOrImmortalTask();
        var t2 = m2.WhenDeadOrImmortalTask();
        
        (m1.IsDead || m1.IsImmortal).AssertIsFalse();
        (m2.IsDead || m2.IsImmortal).AssertIsFalse();
        Task.WhenAny(t1, t2).AssertNotCompleted();
        
        s1.EndLifetime();
        s2.ImmortalizeLifetime();
        
        m1.IsImmortal.AssertIsTrue();
        m2.IsDead.AssertIsTrue();
        Task.WhenAll(t1, t2).AssertRanToCompletion();

        // limbo allows collection
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r = new LifetimeSource();
            var life = r.Lifetime.Opposite();
            life.WhenDeadOrImmortal(a);
            return life;
        });
    }

    [TestMethod]
    public void Min() {
        Lifetime.Immortal.Min(Lifetime.Immortal).IsImmortal.AssertIsTrue();
        Lifetime.Immortal.Min(Lifetime.Dead).IsDead.AssertIsTrue();
        Lifetime.Dead.Min(Lifetime.Immortal).IsDead.AssertIsTrue();
        Lifetime.Dead.Min(Lifetime.Dead).IsDead.AssertIsTrue();

        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var s3 = new LifetimeSource();
        var m1 = s1.Lifetime.Min(s2.Lifetime);
        var m2 = s1.Lifetime.Min(s3.Lifetime);
        var m3 = s2.Lifetime.Min(s3.Lifetime);
        var t1 = m1.WhenDeadOrImmortalTask();
        var t2 = m2.WhenDeadOrImmortalTask();
        var t3 = m3.WhenDeadOrImmortalTask();

        // equality optimization
        s1.Lifetime.Min(s1.Lifetime).AssertEquals(s1.Lifetime);
        m1.Min(m1).AssertEquals(m1);
        // immortality optimization
        s1.Lifetime.Min(Lifetime.Immortal).AssertEquals(s1.Lifetime);
        Lifetime.Immortal.Min(m1).AssertEquals(m1);

        // when one becomes dead, min becomes dead
        (m1.IsDead || m1.IsImmortal).AssertIsFalse();
        (m2.IsDead || m2.IsImmortal).AssertIsFalse();
        Task.WhenAny(t1, t2).AssertNotCompleted();
        s1.EndLifetime();
        m1.IsDead.AssertIsTrue();
        m2.IsDead.AssertIsTrue();
        Task.WhenAll(t1, t2).AssertRanToCompletion();
        
        // when one becomes immortal, min is unaffected
        s2.ImmortalizeLifetime();

        // when both become immortal, min is immortal
        (m3.IsDead || m3.IsImmortal).AssertIsFalse();
        t3.AssertNotCompleted();
        s3.ImmortalizeLifetime();
        m3.IsImmortal.AssertIsTrue();
        t3.AssertRanToCompletion();
        
        // limbo allows collection
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r1 = new LifetimeSource();
            var r2 = new LifetimeSource();
            var r3 = new LifetimeSource();
            var life1 = r1.Lifetime.Min(r2.Lifetime);
            var life2 = r1.Lifetime.Min(r3.Lifetime);
            var life3 = r2.Lifetime.Min(r3.Lifetime);
            life1.WhenDeadOrImmortal(a);
            life2.WhenDeadOrImmortal(a);
            life3.WhenDeadOrImmortal(a);
            r1.ImmortalizeLifetime(); // one immortal has no effect on min going to limbo
            return Tuple.Create(life1, life2, life3);
        });
    }

    [TestMethod]
    public void Max() {
        Lifetime.Dead.Max(Lifetime.Dead).IsDead.AssertIsTrue();
        Lifetime.Dead.Max(Lifetime.Immortal).IsImmortal.AssertIsTrue();
        Lifetime.Immortal.Max(Lifetime.Dead).IsImmortal.AssertIsTrue();
        Lifetime.Immortal.Max(Lifetime.Immortal).IsImmortal.AssertIsTrue();

        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var s3 = new LifetimeSource();
        var m1 = s1.Lifetime.Max(s2.Lifetime);
        var m2 = s1.Lifetime.Max(s3.Lifetime);
        var m3 = s2.Lifetime.Max(s3.Lifetime);
        var t1 = m1.WhenDeadOrImmortalTask();
        var t2 = m2.WhenDeadOrImmortalTask();
        var t3 = m3.WhenDeadOrImmortalTask();

        // equality optimization
        s1.Lifetime.Max(s1.Lifetime).AssertEquals(s1.Lifetime);
        m1.Max(m1).AssertEquals(m1);
        // dead optimization
        s1.Lifetime.Max(Lifetime.Dead).AssertEquals(s1.Lifetime);
        Lifetime.Dead.Max(m1).AssertEquals(m1);

        // when one becomes dead, max becomes dead
        (m1.IsImmortal || m1.IsDead).AssertIsFalse();
        (m2.IsImmortal || m2.IsDead).AssertIsFalse();
        Task.WhenAny(t1, t2).AssertNotCompleted();
        s1.ImmortalizeLifetime();
        m1.IsImmortal.AssertIsTrue();
        m2.IsImmortal.AssertIsTrue();
        Task.WhenAll(t1, t2).AssertRanToCompletion();

        // when one becomes immortal, max is unaffected
        s2.EndLifetime();

        // when both become immortal, max is immortal
        (m3.IsImmortal || m3.IsDead).AssertIsFalse();
        t3.AssertNotCompleted();
        s3.EndLifetime();
        m3.IsDead.AssertIsTrue();
        t3.AssertRanToCompletion();

        // limbo allows collection
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r1 = new LifetimeSource();
            var r2 = new LifetimeSource();
            var r3 = new LifetimeSource();
            var life1 = r1.Lifetime.Max(r2.Lifetime);
            var life2 = r1.Lifetime.Max(r3.Lifetime);
            var life3 = r2.Lifetime.Max(r3.Lifetime);
            life1.WhenDeadOrImmortal(a);
            life2.WhenDeadOrImmortal(a);
            life3.WhenDeadOrImmortal(a);
            r1.EndLifetime(); // one dead has no effect on max going to limbo
            return Tuple.Create(life1, life2, life3);
        });
    }
}
