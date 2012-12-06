using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Util;

[TestClass]
public class DisposableLifetimeTest {
    private static readonly Func<Action> InvalidCallbackMaker = () => {
        var r = new object();
        return () => {
            if (r == new object()) throw new Exception();
            throw new Exception();
        };
    };

    [TestMethod]
    public void DisposableLifetime() {
        var d = new DisposableLifetime();
        d.Lifetime.IsMortal.AssertIsTrue();
        d.Dispose();
        d.Lifetime.IsDead.AssertIsTrue();

        InvalidCallbackMaker.AssertCollectedAfter(a => {
            var r = new DisposableLifetime();
            var life = r.Lifetime;
            life.WhenDead(a);
            return life;
        });
    }
}
