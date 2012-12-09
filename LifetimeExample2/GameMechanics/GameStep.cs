using System;

namespace SnipSnap {
    public class GameStep {
        public readonly TimeSpan TimeStep;
        public GameStep(TimeSpan timeStep) {
            this.TimeStep = timeStep;
        }
    }
}