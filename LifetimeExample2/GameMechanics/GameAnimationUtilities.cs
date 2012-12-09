using System;
using TwistedOak.Util;

namespace LifetimeExample2 {
    public static class GameAnimation {
        public static Lifetime Delay(this Game game, TimeSpan duration) {
            var remaining = duration;
            var life = game.Life.CreateDependentSource();
            game.LoopActions.Add(
                iter => {
                    remaining -= iter.TimeStep;
                    if (remaining < TimeSpan.Zero) life.EndLifetime();
                },
                life.Lifetime);
            return life.Lifetime;
        }

        public delegate void AnimationCallback(GameStep gameStep, double proportionCompleted, TimeSpan elapsed);
        public static Lifetime AnimateWith(this Game game, TimeSpan duration, AnimationCallback callback, Lifetime? constraint = default(Lifetime?)) {
            var remaining = duration;
            var life = (constraint ?? game.Life).CreateDependentSource();
            game.LoopActions.Add(
                iter => {
                    remaining -= iter.TimeStep;
                    if (remaining >= TimeSpan.Zero) {
                        callback(iter, 1 - remaining.TotalSeconds / duration.TotalSeconds, remaining);
                    } else {
                        life.EndLifetime();
                    }
                },
                life.Lifetime);
            return life.Lifetime;
        }
    }
}