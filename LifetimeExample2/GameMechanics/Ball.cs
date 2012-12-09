using System;
using System.Windows;
using TwistedOak.Util;

namespace SnipSnap {
    public class Ball {
        public Point LastPos;
        public Point Pos;
        public Vector Vel;
        public double Radius;
        public double Hue;
        public LifetimeSource Life;
        public int Generation;
        public TimeSpan? Death;
    }
}