using System;
using System.Linq;
using System.Threading;
using TwistedOak.Util;
using System.Reactive.Linq;

public static class Program {
    private static void WriteLine(this string text, params object[] arg) {
        Console.WriteLine(text, arg);
    }
    private static void Break() {
        Console.WriteLine("---");
        Console.ReadLine();
    }

    public static void Main() {
        var sourceCollection = new PerishableCollection<int>();
        var uniqueId = 0;
        var transformedCollection = sourceCollection
            // treat collection as an observable sequence of perishable items, so we can easily transform it
            .AsObservable()
            // pair each item with a unique id
            // using perishables means we don't care that the projection can't be repeated!
            .Select((int e) => Tuple.Create(e, Interlocked.Increment(ref uniqueId)))
            // ignore the first two items in the collection (either already in it or added to it) when we observe it
            .Skip(2)
            // ignore items whose value matches their unique id
            .Where(e => e.Item1 != e.Item2)
            // accumulate items into a new perishable collection (never stops observing the source collection)
            .ToPerishableCollection(Lifetime.Immortal);

        var lifeSources = Enumerable.Range(0, 20).Select(e => new LifetimeSource()).ToArray();
        var lifes = lifeSources.Select(e => e.Lifetime).ToArray();
        sourceCollection.Add(13, lifes[0]); // -> (13, 1) -> skipped
        sourceCollection.Add(5, lifes[1]); // -> (5, 2) -> skipped
        sourceCollection.Add(7, lifes[2]); // -> (7, 3) -> kept -> pass -> added
        sourceCollection.Add(4, lifes[3]); // -> (4, 4) -> kept -> fail
        sourceCollection.Add(7, lifes[4]); // -> (7, 5) -> kept -> pass -> added

        lifeSources[0].EndLifetime(); // removes 13 from the collection, no effect on transformed collection
        lifeSources[2].EndLifetime(); // removes the first 7 from the collection, and (7, 3) from the transformed collection

        "=== Hit 'Enter' to advance ===".WriteLine();
        Break();

        ///////////////////////////////////////////////////
        "Manually inspecting a lifetime as it is killed:".WriteLine();
        var lifesource1 = new LifetimeSource();
        "Life #1: {0}".WriteLine(lifesource1);
        "Ending Life #1".WriteLine();
        lifesource1.EndLifetime();
        "Life #1: {0}".WriteLine(lifesource1);
        Break();

        ///////////////////////////////////////////////////
        "Manually inspecting a lifetime as it is immortalized:".WriteLine();
        var lifesource2 = new LifetimeSource();
        "Life #2: {0}".WriteLine(lifesource2);
        "Immortalizing Life #2".WriteLine();
        lifesource2.ImmortalizeLifetime();
        "Life #2: {0}".WriteLine(lifesource2);
        Break();

        ///////////////////////////////////////////////////
        "Using callbacks to inspect a lifetime as it is killed:".WriteLine();
        var lifesource3 = new LifetimeSource();
        "Life #3: {0}".WriteLine(lifesource3);
        "Registering WhenDead Life #3 (before)".WriteLine();
        lifesource3.Lifetime.WhenDead(() => "WhenDead Life #3 (before)".WriteLine());
        "Ending Life #3".WriteLine();
        lifesource3.EndLifetime();
        "Life #3: {0}".WriteLine(lifesource3);
        "Registering WhenDead Life #3 (after)".WriteLine();
        lifesource3.Lifetime.WhenDead(() => "WhenDead Life #3 (after)".WriteLine());
        Break();

        ///////////////////////////////////////////////////
        "Using conditional callbacks:".WriteLine();
        var lifesource4 = new LifetimeSource();
        var lifesource5 = new LifetimeSource();
        "Life #4: {0}".WriteLine(lifesource4);
        "Life #5: {0}".WriteLine(lifesource5);
        "Registering WhenDead 4 (requires 5)".WriteLine();
        lifesource4.Lifetime.WhenDead(
            () => "Running WhenDead 4 (requires 5)".WriteLine(),
            lifesource5.Lifetime);
        "Registering WhenDead 5 (requires 4)".WriteLine();
        lifesource5.Lifetime.WhenDead(
            () => "Running WhenDead 5 (requires 4)".WriteLine(),
            lifesource4.Lifetime);
        "Ending Life #4".WriteLine();
        lifesource4.EndLifetime();
        "Life #4: {0}".WriteLine(lifesource4);
        "Ending Life #5".WriteLine();
        lifesource5.EndLifetime();
        "Life #5: {0}".WriteLine(lifesource5);
        Break();

        ///////////////////////////////////////////////////
        CallbackGarbageTest(
            "Immortal Lifetime collectable callback test:",
            () => new LifetimeSource().Lifetime,
            (e, a) => e.WhenDead(a));
        ///////////////////////////////////////////////////
        var lifetimeMortal = new LifetimeSource();
        CallbackGarbageTest(
            "Conditioned-on-dying Lifetime collectable callback test:",
            () => lifetimeMortal.Lifetime,
            (e, a) => {
                var r = new LifetimeSource();
                e.WhenDead(a, r.Lifetime);
                r.EndLifetime();
            });
        GC.KeepAlive(lifetimeMortal);
        ///////////////////////////////////////////////////
        CallbackGarbageTest(
            "Immortal CancellationToken collectable callback test:",
            () => new CancellationTokenSource().Token,
            (e, a) => e.Register(a));

        ///////////////////////////////////////////////////
        "Garbage collection of a lifetime's source:".WriteLine();
        var lifesource6 = new LifetimeSource();
        var life6 = lifesource6.Lifetime;
        "Life #6: {0}".WriteLine(life6);
        "Allowing garbage collection of Life #6's source".WriteLine();
        lifesource6 = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        "Life #6: {0}".WriteLine(life6);
        Break();

        ///////////////////////////////////////////////////
        "=== Hit 'Enter' to close ===".WriteLine();
        Break();
    }

    private static void CallbackGarbageTest<T>(string name, Func<T> lostTokenMaker, Action<T, Action> callbackRegistrar) {
        // create a lifetime/token whose source can be garbage collected
        // (meaning the lifetime/token will never be cancelled/ended)
        var lostToken = lostTokenMaker();

        Console.WriteLine(name);
        var n1 = GC.GetTotalMemory(forceFullCollection: true) / Math.Pow(10, 6);
        "Memory before: {0:0.000} MB".WriteLine(n1);

        // do allocations inside an anonymous method, to avoid the debugger "helpfully" keeping the last few around
        new Action(() => {
            // register many callbacks with closures containing large amounts of memory be run when the lifetime/token ends/cancels
            // (these callbacks will never run; the sources are collected)
            for (var i = 0; i < 2000; i++) {
                var r = new byte[2000];
                callbackRegistrar(
                    lostToken,
                    () => { // never actually runs
                        r[0] += 1; // large amount of memory captured by closure
                        throw new InvalidOperationException();
                    });
            }
        }).Invoke();

        var n2 = GC.GetTotalMemory(forceFullCollection: true) / Math.Pow(10, 6);
        "Memory after:  {0:0.000} MB".WriteLine(n2);
        Break();
        GC.KeepAlive(lostToken);
    }
}
