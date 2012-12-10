using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TwistedOak.Collections;
using TwistedOak.Util;

[TestClass]
public class PerishableCollectionTest {
    [TestMethod]
    public void IsEnumerable() {
        var source = new LifetimeSource();
        var p1 = new Perishable<int>(1, source.Lifetime);
        var p2 = new Perishable<int>(1, Lifetime.Immortal);
        var p = new PerishableCollection<int>();
        p.CurrentItems().AssertSequenceEquals();

        p.Add(p1);
        p.CurrentItems().AssertSequenceEquals(p1);

        p.Add(p2.Value, p2.Lifetime);
        p.CurrentItems().AssertSequenceEquals(p1, p2);
        
        source.EndLifetime();
        p.CurrentItems().AssertSequenceEquals(p2);
    }
    [TestMethod]
    public void IsObservable() {
        var source = new LifetimeSource();
        var p1 = new Perishable<int>(1, source.Lifetime);
        var p2 = new Perishable<int>(1, Lifetime.Immortal);
        var p = new PerishableCollection<int>();
        
        var li0 = new List<Perishable<int>>();
        p.AsObservable().Subscribe(e => {
            li0.Add(e);
            e.Lifetime.WhenDead(() => li0.Remove(e));
        });
        li0.AssertSequenceEquals();

        p.Add(p1);
        li0.AssertSequenceEquals(p1);
        
        var li1 = new List<Perishable<int>>();
        p.AsObservable().Subscribe(e => {
            li1.Add(e);
            e.Lifetime.WhenDead(() => li1.Remove(e));
        });
        li1.AssertSequenceEquals(p1);
        
        p.Add(p2.Value, p2.Lifetime);
        li0.AssertSequenceEquals(p1, p2);
        li1.AssertSequenceEquals(p1, p2);
        
        source.EndLifetime();
        li0.AssertSequenceEquals(p2);
        li1.AssertSequenceEquals(p2);
    }
    [TestMethod]
    public void IsResultThreadSafe() {
        foreach (var repeat in Enumerable.Range(0, 5)) {
            var n = 5000;
            var nt = 4;
            var p = new PerishableCollection<int>();
            var queues = Enumerable.Range(0, 4).Select(e => new Queue<LifetimeSource>()).ToArray();
            TestUtil.ConcurrencyTest(
                threadCount: nt,
                callbackCount: n,
                repeatedWork: (t, i) => {
                    var r = new LifetimeSource();
                    p.Add(i, r.Lifetime);
                    if (i % 2 == 0) queues[t].Enqueue(r);
                    if (queues[t].Count > 20)
                        queues[t].Dequeue().EndLifetime();
                }, 
                finalWork: t => {
                    while (queues[t].Count > 0)
                        queues[t].Dequeue().EndLifetime();
                });
            
            var expected = Enumerable.Range(0, n).Where(e => e%2 != 0).SelectMany(e => Enumerable.Repeat(e, nt));
            p.CurrentItems().OrderBy(e => e.Value).Select(e => e.Value).AssertSequenceEquals(expected);
        }
    }
    [TestMethod]
    public void IsObservationThreadSafe() {
        foreach (var repeat in Enumerable.Range(0, 5)) {
            var n = 5000;
            var nt = 4;
            var p = new PerishableCollection<int>();
            var queues = Enumerable.Range(0, 4).Select(e => new Queue<LifetimeSource>()).ToArray();
            var li = new List<int>();
            p.AsObservable().Subscribe(
                e => {
                    lock (li) li.Add(e.Value);
                    e.Lifetime.WhenDead(() => { lock (li) li.Remove(e.Value); });
                });
            TestUtil.ConcurrencyTest(
                threadCount: nt,
                callbackCount: n,
                repeatedWork: (t, i) => {
                    var r = new LifetimeSource();
                    p.Add(i, r.Lifetime);
                    if (i % 2 == 0) queues[t].Enqueue(r);
                    if (queues[t].Count > 20)
                        queues[t].Dequeue().EndLifetime();
                },
                finalWork: t => {
                    while (queues[t].Count > 0)
                        queues[t].Dequeue().EndLifetime();
                });

            var expected = Enumerable.Range(0, n).Where(e => e % 2 != 0).SelectMany(e => Enumerable.Repeat(e, nt));
            li.OrderBy(e => e).AssertSequenceEquals(expected);
        }
    }
    [TestMethod]
    public void IsEnumeratingWhileMutatingThreadSafe() {
        foreach (var repeat in Enumerable.Range(0, 5)) {
            var n = 5000;
            var nt = 4;
            var p = new PerishableCollection<int>();
            var queues = Enumerable.Range(0, 4).Select(e => new Queue<LifetimeSource>()).ToArray();
            TestUtil.ConcurrencyTest(
                threadCount: nt,
                callbackCount: n,
                repeatedWork: (t, i) => {
                    if (t%2 == 0) {
                        if (i%500 != 0) return;
                        var r = p.CurrentItems().OrderBy(e => e.Value).ToList();
                        (r.Count(e => e.Value%2 == 0 && !e.Lifetime.IsDead) < nt/2 * 20).AssertIsTrue();

                    } else {
                        var r = new LifetimeSource();
                        p.Add(i, r.Lifetime);
                        if (i % 2 == 0) queues[t].Enqueue(r);
                        if (queues[t].Count > 20)
                            queues[t].Dequeue().EndLifetime();
                    }
                },
                finalWork: t => {
                    while (queues[t].Count > 0)
                        queues[t].Dequeue().EndLifetime();
                });
        }
    }
}
