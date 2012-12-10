using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Collections;
using TwistedOak.Util;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

[TestClass]
public class PerishableUtilitiesTest {
    [TestMethod]
    public void ToPerishableCollection() {
        var source = new LifetimeSource();
        var p1 = new Perishable<int>(1, source.Lifetime);
        var p2 = new Perishable<int>(1, Lifetime.Immortal);
        var p = new PerishableCollection<int>();
        var q = p.AsObservable().ToPerishableCollection();
        q.CurrentItems().AssertSequenceEquals();

        p.Add(p1);
        q.CurrentItems().AssertSequenceEquals(p1);

        p.Add(p2.Value, p2.Lifetime);
        q.CurrentItems().AssertSequenceEquals(p1, p2);

        source.EndLifetime();
        q.CurrentItems().AssertSequenceEquals(p2);
    }
    [TestMethod]
    public void PerishableObservableSelect() {
        new[] { new Perishable<int>(1, Lifetime.Immortal) }
            .ToObservable()
            .Select(e => e + 1)
            .ToList()
            .ToTask()
            .AssertRanToCompletion()
            .AssertSequenceEquals(new Perishable<int>(2, Lifetime.Immortal));
    }
    [TestMethod]
    public void PerishableObservableWhere() {
        new[] { new Perishable<int>(1, Lifetime.Immortal), new Perishable<int>(2, Lifetime.Immortal) }
            .ToObservable()
            .Where(e => e != 1)
            .ToList()
            .ToTask()
            .AssertRanToCompletion()
            .AssertSequenceEquals(new Perishable<int>(2, Lifetime.Immortal));
    }
    [TestMethod]
    public void PerishableEnumerableSelect() {
        new[] { new Perishable<int>(1, Lifetime.Immortal) }.Select(e => e + 1).AssertSequenceEquals(new Perishable<int>(2, Lifetime.Immortal));
    }
    [TestMethod]
    public void PerishableEnumerableWhere() {
        new[] {
            new Perishable<int>(1, Lifetime.Immortal),
            new Perishable<int>(2, Lifetime.Immortal)
        }.Where(e => e != 1).AssertSequenceEquals(new Perishable<int>(2, Lifetime.Immortal));
    }
    [TestMethod]
    public void ObserveNonPerishedCount() {
        var li1 = new List<int>();
        new[] { new Perishable<int>(1, Lifetime.Immortal), new Perishable<int>(2, Lifetime.Immortal) }
            .ToObservable()
            .ObserveNonPerishedCount(completeWhenSourceCompletes: true)
            .Subscribe(li1.Add, () => li1.Add(-1));
        li1.AssertSequenceEquals(0, 1, 2, -1);

        var source = new LifetimeSource();
        var li2 = new List<int>();
        new[] { new Perishable<int>(1, source.Lifetime), new Perishable<int>(2, source.Lifetime) }
            .ToObservable()
            .ObserveNonPerishedCount(completeWhenSourceCompletes: false)
            .Subscribe(li2.Add, () => li2.Add(-1));
        li2.AssertSequenceEquals(0, 1, 2);
        source.EndLifetime();
        li2.AssertSequenceEquals(0, 1, 2, 1, 0, -1);
    }
    [TestMethod]
    public void PerishableGroupBy() {
        var s1 = new LifetimeSource();
        var s2 = new LifetimeSource();
        var s3 = new LifetimeSource();
        var p1 = new Perishable<int>(1, s1.Lifetime);
        var p2 = new Perishable<int>(2, s2.Lifetime);
        var p3 = new Perishable<int>(1, s3.Lifetime);
        
        var li = new List<Perishable<KeyValuePair<int, IObservable<Perishable<int>>>>?>();
        new[] { p1, p2, p3 }
            .ToObservable()
            .PerishableGroupBy(e => e)
            .Subscribe(e => li.Add(e), () => li.Add(null));
        
        li.Count.AssertEquals(3);
        if (!li[0].HasValue) Assert.Fail();
        if (!li[1].HasValue) Assert.Fail();
        var v0 = li[0].Value;
        var v1 = li[1].Value;
        li[2].HasValue.AssertIsFalse();
        v0.Value.Key.AssertEquals(1);
        v1.Value.Key.AssertEquals(2);

        var li1 = new List<Perishable<int>?>();
        var li2 = new List<Perishable<int>?>();
        v0.Value.Value.Subscribe(e => li1.Add(e), () => li1.Add(null));
        v1.Value.Value.Subscribe(e => li2.Add(e), () => li2.Add(null));
        li1.AssertSequenceEquals(p1, p3, null);
        li2.AssertSequenceEquals(p2, null);

        v0.Lifetime.IsMortal.AssertIsTrue();
        v1.Lifetime.IsMortal.AssertIsTrue();

        s1.EndLifetime();
        v0.Lifetime.IsMortal.AssertIsTrue();
        v1.Lifetime.IsMortal.AssertIsTrue();

        s3.EndLifetime();
        v0.Lifetime.IsDead.AssertIsTrue();
        v1.Lifetime.IsMortal.AssertIsTrue();

        s2.EndLifetime();
        v1.Lifetime.IsDead.AssertIsTrue();
        
        li.Count.AssertEquals(3);
    }
}
