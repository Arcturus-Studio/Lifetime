using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SnipSnap.Mathematics;
using TwistedOak.Util;

namespace SnipSnap {
    ///<summary>Generic utility methods for working with the game.</summary>
    public static class GameUtilities {
        ///<summary>Runs the game loop until the game ends.</summary>
        public async static Task Loop(this Game game) {
            var clock = new Stopwatch();
            clock.Start();

            var lastTime = clock.Elapsed;
            while (!game.Life.IsDead) {
                var dt = clock.Elapsed - lastTime;
                lastTime += dt;

                var step = new GameStep(timeStep: dt.Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
                foreach (var e in game.LoopActions.CurrentItems())
                    e.Value.Invoke(step);

                await Task.Delay(30.Milliseconds());
            }
        }

        ///<summary>A lifetime that ends after the given duration has elapsed, in game time.</summary>
        public static Lifetime Delay(this Game game, TimeSpan duration) {
            var remaining = duration;
            var life = game.Life.CreateDependentSource();
            game.LoopActions.Add(
                step => {
                    remaining -= step.TimeStep;
                    if (remaining < TimeSpan.Zero) life.EndLifetime();
                },
                life.Lifetime);
            return life.Lifetime;
        }

        ///<summary>Given progress data about an animation during each iteration of the game loop.</summary>
        public delegate void AnimationCallback(GameStep gameStep, double proportionCompleted, TimeSpan elapsed);

        /// <summary>
        /// Manages tracking the progress of an animation, running a callback with the information.
        /// Returns a lifetime that ends when the animation has expired.
        /// </summary>
        public static Lifetime AnimateWith(this Game game, TimeSpan duration, AnimationCallback callback, Lifetime? constraint = default(Lifetime?)) {
            var remaining = duration;
            var life = (constraint ?? game.Life).CreateDependentSource();
            game.LoopActions.Add(
                step => {
                    remaining -= step.TimeStep;
                    if (remaining >= TimeSpan.Zero) {
                        callback(step, 1 - remaining.TotalSeconds / duration.TotalSeconds, duration - remaining);
                    } else {
                        life.EndLifetime();
                    }
                },
                life.Lifetime);
            return life.Lifetime;
        }
    }
}