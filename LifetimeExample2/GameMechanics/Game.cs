using System;
using TwistedOak.Util;

namespace SnipSnap {
    public class Game {
        ///<summary>Each non-perished action in this collection is run during each iteration of the game loop.</summary>
        public readonly PerishableCollection<Action<GameStep>> LoopActions = new PerishableCollection<Action<GameStep>>();
        ///<summary>The active balls in the game.</summary>
        public readonly PerishableCollection<Ball> Balls = new PerishableCollection<Ball>();
        ///<summary>The active ball connectors in the game.</summary>
        public readonly PerishableCollection<Connector> Connectors = new PerishableCollection<Connector>();
        ///<summary>The lifetime source of the game, that controls when the game is ended.</summary>
        public readonly LifetimeSource LifeSource = new LifetimeSource();
        ///<summary>The lifetime of the game, that ends when the game should be stopped.</summary>
        public Lifetime Life { get { return this.LifeSource.Lifetime; } }
        ///<summary>A common random number generator.</summary>
        public readonly Random Rng = new Random();
    }
}