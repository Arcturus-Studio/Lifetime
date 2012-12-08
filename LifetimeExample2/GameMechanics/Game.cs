using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LifetimeExample.Mathematics;
using TwistedOak.Util;

namespace LifetimeExample2 {
    public class Game {
        public readonly PerishableCollection<Action<Iter>> LoopActions = new PerishableCollection<Action<Iter>>();
        public readonly PerishableCollection<Ball> Balls = new PerishableCollection<Ball>();
        public readonly PerishableCollection<BallLine> Connectors = new PerishableCollection<BallLine>();
        public readonly LifetimeSource LifeSource = new LifetimeSource();
        public Lifetime Life { get { return this.LifeSource.Lifetime; } }
        public readonly Random Rng = new Random();

        public async void Loop() {
            var clock = new Stopwatch();
            clock.Start();
            var lastTime = clock.Elapsed;
            while (true) {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
                if (this.Life.IsDead) break;
                var dt = clock.Elapsed - lastTime;
                lastTime += dt;
                var smoothedDt = dt.Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(1));

                foreach (var e in this.LoopActions.CurrentItems())
                    e.Value.Invoke(new Iter { dt = smoothedDt });
            }
        }
   }
}