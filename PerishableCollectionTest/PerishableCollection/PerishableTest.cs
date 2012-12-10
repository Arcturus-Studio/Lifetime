using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Collections;
using TwistedOak.Util;

[TestClass]
public class PerishableTest {
    [TestMethod]
    public void ConstructionProperties() {
        var source = new LifetimeSource();
        var p = new Perishable<int>(2, source.Lifetime);
        p.Value.AssertEquals(2);
        p.Lifetime.AssertEquals(source.Lifetime);
    }
    [TestMethod]
    public void Equality() {
        var source = new LifetimeSource();
        var p = new Perishable<int>(2, source.Lifetime);
        p.AssertEquals(new Perishable<int>(2, source.Lifetime));
        p.Equals(new Perishable<int>(3, source.Lifetime)).AssertIsFalse();
        p.Equals(new Perishable<int>(2, Lifetime.Immortal)).AssertIsFalse();
    }
}
