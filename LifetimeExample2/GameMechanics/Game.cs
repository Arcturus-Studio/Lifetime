using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LifetimeExample.Mathematics;
using TwistedOak.Util;

namespace LifetimeExample2 {
    public class Game {
        public readonly PerishableCollection<Action<GameStep>> LoopActions = new PerishableCollection<Action<GameStep>>();
        public readonly PerishableCollection<Ball> Balls = new PerishableCollection<Ball>();
        public readonly PerishableCollection<Connector> Connectors = new PerishableCollection<Connector>();
        public readonly LifetimeSource LifeSource = new LifetimeSource();
        public Lifetime Life { get { return this.LifeSource.Lifetime; } }
        public readonly Random Rng = new Random();

        public async void Loop() {
            var clock = new Stopwatch();
            clock.Start();
            
            var lastTime = clock.Elapsed;
            while (!Life.IsDead) {
                var dt = clock.Elapsed - lastTime;
                lastTime += dt;

                var step = new GameStep(timeStep: dt.Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
                foreach (var e in LoopActions.CurrentItems())
                    e.Value.Invoke(step);
                
                await Task.Delay(30.Milliseconds());
            }
        }
   }
}