using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

internal static class TestUtil {
    private static void TestWait(this Task t, TimeSpan timeout) {
        Task.WhenAny(t, Task.Delay(timeout)).Wait();
        t.Wait(new TimeSpan(1));
    }
    [DebuggerStepThrough]
    public static void AssertEquals<T>(this T expected, T actual) {
        Assert.AreEqual(actual, expected);
    }
    [DebuggerStepThrough]
    public static void AssertSequenceEquals<T>(this IEnumerable<T> actual, IEnumerable<T> expected) {
        var r1 = actual.ToArray();
        var r2 = expected.ToArray();
        if (!r1.SequenceEqual(r2))
            Assert.Fail("Sequences not equal. Expected: {0}; Actual: {1}", String.Join(",", r2), String.Join(",", r1));
    }
    [DebuggerStepThrough]
    public static void AssertIsTrue(this bool b) {
        Assert.IsTrue(b);
    }
    [DebuggerStepThrough]
    public static void AssertIsFalse(this bool b) {
        Assert.IsFalse(b);
    }
    public static void ExpectException<TException>(Action action) where TException : Exception {
        try {
            action();
        } catch (TException) {
            return;
        }
        throw new InvalidOperationException("Expected an exception.");
    }
    [DebuggerStepThrough]
    public static void AssertRanToCompletion(this Task t, TimeSpan? timeout = null) {
        t.TestWait(timeout ?? TimeSpan.FromSeconds(10));
        Assert.IsTrue(t.IsCompleted);
    }
    [DebuggerStepThrough]
    public static T AssertRanToCompletion<T>(this Task<T> t, TimeSpan? timeout = null) {
        t.TestWait(timeout ?? TimeSpan.FromSeconds(10));
        t.IsCompleted.AssertIsTrue();
        return t.Result;
    }
    [DebuggerStepThrough]
    public static void AssertFailed<TException>(this Task t, TimeSpan? timeout = null) where TException : Exception {
        try {
            t.TestWait(timeout ?? TimeSpan.FromSeconds(5));
            Assert.Fail("Expected an exception of type " + typeof(TException).FullName + ", but ran succesfully.");
        } catch (TException) {
        } catch (Exception ax) {
            var ex = ax is AggregateException ? ((AggregateException)ax).Flatten() : ax;
            if (ex is TException) return;
            Assert.Fail("Expected an exception of type " + typeof(TException).FullName + ", but got a different one: " + ex);
        }
    }
    [DebuggerStepThrough]
    public static void AssertCancelled(this Task t, TimeSpan? timeout = null) {
        try {
            t.TestWait(timeout ?? TimeSpan.FromSeconds(5));
        } catch (Exception) {
        }
        t.IsCanceled.AssertIsTrue();
    }
    [DebuggerStepThrough]
    public static void AssertNotCompleted(this Task t, TimeSpan? timeout = null) {
        t.TestWait(timeout ?? TimeSpan.FromSeconds(0.05));
        t.IsCompleted.AssertIsFalse();
    }
    public static void AssertCollectedAfter<T>(this Func<T> collectableValueMaker, Action<T> doAndNoHold) where T : class {
        AssertCollectedAfter(collectableValueMaker, e => {
            doAndNoHold(e);
            return null;
        });
    }
    public static void AssertNotCollectedAfter<T>(this Func<T> collectableValueMaker, Action<T> doAndNoHold) where T : class {
        AssertNotCollectedAfter(collectableValueMaker, e => {
            doAndNoHold(e);
            return null;
        });
    }
    private static void Clear<T>(ref T value) {
        if (!ReferenceEquals(default(T), value)) 
            value = default(T);
    }
    public static void AssertCollectedAfter<T>(this Func<T> collectableValueMaker, Func<T, object> doAndHold) where T : class {
        var value = collectableValueMaker();
        var r = new WeakReference<T>(value);
        var held = doAndHold(value);
        Clear(ref value);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        r.TryGetTarget(out value).AssertIsFalse();
        GC.KeepAlive(held);
    }
    public static void AssertNotCollectedAfter<T>(this Func<T> collectableValueMaker, Func<T, object> doAndHold) where T : class {
        var value = collectableValueMaker();
        var r = new WeakReference<T>(value);
        var held = doAndHold(value);
        Clear(ref value);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        r.TryGetTarget(out value).AssertIsTrue();
        GC.KeepAlive(held);
    }
}
