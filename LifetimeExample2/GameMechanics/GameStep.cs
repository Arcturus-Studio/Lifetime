using System;

namespace LifetimeExample2 {
    public class GameStep {
        public readonly TimeSpan TimeStep;
        public GameStep(TimeSpan timeStep) {
            this.TimeStep = timeStep;
        }
    }
}