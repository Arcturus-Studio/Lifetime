using System;
using System.Diagnostics;
using System.Windows;

namespace SnipSnap.Mathematics {
    [DebuggerDisplay("{ToString()}")]
    public struct LineSegment {
        public readonly Point Start;
        public readonly Vector Delta;
        public Point Mid { get { return Start + Delta/2; } }
        public Point End { get { return this.Start + this.Delta; } }
        public LineSegment(Point start, Vector delta) {
            this.Start = start;
            this.Delta = delta;
        }
        public LineSegment(Point start, Point end) {
            this.Start = start;
            this.Delta = end - start;
        }
        public override string ToString() {
            return String.Format("{0}:{1}", this.Start, this.End);
        }
    }
}