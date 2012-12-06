using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

internal class LifetimeExchangerTest {
    private static readonly Func<Action> InvalidCallbackMaker = () => () => { throw new Exception(); };
    [TestMethod]
    public void StartNextAndEndPreviousLifetime() {
        var r = new LifetimeExchanger();

        var life1 = r.StartNextAndEndPreviousLifetime();
        (life1.IsDead || life1.IsImmortal).AssertIsFalse();

        var life2 = r.StartNextAndEndPreviousLifetime();
        life1.IsDead.AssertIsTrue();
        (life2.IsDead || life2.IsImmortal).AssertIsFalse();

        var life3 = r.StartNextAndEndPreviousLifetime();
        life2.IsDead.AssertIsTrue();
        (life3.IsDead || life3.IsImmortal).AssertIsFalse();
    }
    [TestMethod]
    public void StartNextAndImmortalizePreviousLifetime() {
        var r = new LifetimeExchanger();

        var life1 = r.StartNextAndImmortalizePreviousLifetime();
        (life1.IsImmortal || life1.IsImmortal).AssertIsFalse();

        var life2 = r.StartNextAndImmortalizePreviousLifetime();
        life1.IsImmortal.AssertIsTrue();
        (life2.IsDead || life2.IsImmortal).AssertIsFalse();

        var life3 = r.StartNextAndImmortalizePreviousLifetime();
        life2.IsImmortal.AssertIsTrue();
        (life3.IsDead || life3.IsImmortal).AssertIsFalse();
    }
    [TestMethod]
    public void ImmortalizePreviousLifetime() {
        var r = new LifetimeExchanger();
        r.ImmortalizePreviousLifetime(); // no effect

        var life1 = r.StartNextAndImmortalizePreviousLifetime();
        (life1.IsImmortal || life1.IsImmortal).AssertIsFalse();

        r.ImmortalizePreviousLifetime();
        life1.IsImmortal.AssertIsTrue();
        r.ImmortalizePreviousLifetime(); // no effect
    }
    [TestMethod]
    public void EndPreviousLifetime() {
        var r = new LifetimeExchanger();
        r.EndPreviousLifetime(); // no effect

        var life1 = r.StartNextAndImmortalizePreviousLifetime();
        (life1.IsImmortal || life1.IsImmortal).AssertIsFalse();

        r.EndPreviousLifetime();
        life1.IsDead.AssertIsTrue();
        r.EndPreviousLifetime(); // no effect
    }
    [TestMethod]
    public void ExchangerLimbo() {
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r = new LifetimeExchanger();
            var life = r.StartNextAndEndPreviousLifetime();
            life.WhenDeadOrImmortal(a);
            return life;
        });
    }
}
