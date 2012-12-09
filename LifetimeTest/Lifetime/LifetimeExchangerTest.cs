using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Strilanc.Util;

[TestClass]
public class LifetimeExchangerTest {
    private static readonly Func<Action> InvalidCallbackMaker = () => {
        var r = new object();
        return () => {
            if (r == new object()) throw new Exception();
            throw new Exception();
        };
    };

    [TestMethod]
    public void Exchanger() {
        var r = new LifetimeExchanger();
        var life1 = r.ActiveLifetime;
        life1.IsMortal.AssertIsTrue();

        var life2 = r.StartNextAndEndPreviousLifetime();
        r.ActiveLifetime.AssertEquals(life2);
        life1.IsDead.AssertIsTrue();
        life2.IsMortal.AssertIsTrue();

        var life3 = r.StartNextAndImmortalizePreviousLifetime();
        r.ActiveLifetime.AssertEquals(life3);
        life2.IsImmortal.AssertIsTrue();
        life3.IsMortal.AssertIsTrue();
    }
    
    [TestMethod]
    public void ExchangerLimbo() {
        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r = new LifetimeExchanger();
            var life = r.StartNextAndEndPreviousLifetime();
            life.WhenDead(a);
            return life;
        });
    }

    [TestMethod]
    public void ExchangerConcurrency() {
        var repeats = 20;
        foreach (var repeat in Enumerable.Range(0, repeats)) {
            var n = 0;
            var exchanger = new LifetimeExchanger();
            TestUtil.ConcurrencyTest(
                threadCount: 4,
                callbackCount: 10000,
                repeatedWork: (t, i) => {
                    exchanger.ActiveLifetime.WhenDead(() => Interlocked.Increment(ref n));
                    exchanger.StartNextAndEndPreviousLifetime();
                });
            n.AssertEquals(10000*4);
        }
    }

    [TestMethod]
    public void ExchangerRaces() {
        // the goal of this test is to try to trigger a race condition in how lifetime subscription lists are handled
        var repeats = 20;
        foreach (var repeat in Enumerable.Range(0, repeats)) {
            var n = 0;
            var exchangers = new[] {new LifetimeExchanger(), new LifetimeExchanger()};
            TestUtil.ConcurrencyTest(
                threadCount: 4,
                callbackCount: 10000,
                repeatedWork: (t, i) => {
                    // callback run races against callback remove
                    exchangers[0].ActiveLifetime.WhenDead(() => Interlocked.Increment(ref n), exchangers[1].ActiveLifetime);
                    exchangers[t%2].StartNextAndEndPreviousLifetime();
                });
            // the final value of n depends on a ton of race conditions, so we can't depend on it
        }
    }
}
