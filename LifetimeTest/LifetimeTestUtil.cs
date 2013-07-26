using System.Threading.Tasks;
using TwistedOak.Util;

internal static class LifetimeTestUtil {
    public static Task WhenDeadTask(this Lifetime lifetime, Lifetime r = default(Lifetime)) {
        var t = new TaskCompletionSource<bool>();
        lifetime.WhenDead(() => t.SetResult(true), r);
        return t.Task;
    }
}