using System;
using System.Threading;
using TwistedOak.Util;

static class Program {
    static void PrintEventualResult(this Lifetime lifetime, string name) {
        lifetime.WhenDead(() => Console.WriteLine("{0}: {1}", name, lifetime));
    }
    static void Main() {
        // prints 'dead'
        var lifesource1 = new LifetimeSource();
        lifesource1.Lifetime.PrintEventualResult("Life #1");
        lifesource1.EndLifetime();

        // prints 'immortal'
        var lifesource2 = new LifetimeSource();
        lifesource2.Lifetime.PrintEventualResult("Life #2");
        lifesource2.EndLifetime();

        Console.WriteLine("Memory: {0:0.000} MB", GC.GetTotalMemory(forceFullCollection: true) / Math.Pow(10, 6));
        var limboLifetime = MakeLifetimeWithCollectedSourceAndManyCallbacks();
        Console.WriteLine("Memory after living in limbo: {0:0.000} MB", GC.GetTotalMemory(forceFullCollection: true) / Math.Pow(10, 6));
        var limboCancellationToken = MakeCancelTokenWithCollectedSourceAndManyCallbacks();
        Console.WriteLine("Memory after cancelling in limbo: {0:0.000} MB", GC.GetTotalMemory(forceFullCollection: true) / Math.Pow(10, 6));

        Console.Read();
        GC.KeepAlive(limboLifetime);
        GC.KeepAlive(limboCancellationToken);
    }
    private static Lifetime MakeLifetimeWithCollectedSourceAndManyCallbacks() {
        var limbo = new Func<Lifetime>(
            () => new LifetimeSource().Lifetime).Invoke();
        for (var i = 0; i < 2000; i++) {
            var r = new byte[2000 - 91];
            limbo.WhenDead(() => { // never actually runs
                r[0] += 1; // large amount of memory captured by closure
                throw new InvalidOperationException();
            });
        }
        return limbo;
    }
    private static CancellationToken MakeCancelTokenWithCollectedSourceAndManyCallbacks() {
        var limbo = new Func<CancellationToken>(
            () => new CancellationTokenSource().Token).Invoke();
        for (var i = 0; i < 2000; i++) {
            var r = new byte[2000 - 91];
            limbo.Register(() => { // never actually runs
                r[0] += 1; // large amount of memory captured by closure
                throw new InvalidOperationException();
            });
        }
        return limbo;
    }
}
