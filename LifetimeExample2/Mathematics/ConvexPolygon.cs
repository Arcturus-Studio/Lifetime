using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace LifetimeExample.Mathematics {
    [DebuggerDisplay("{ToString()}")]
    public struct ConvexPolygon {
        public readonly IReadOnlyList<Point> Corners;
        public IEnumerable<LineSegment> Edges { get { return this.Corners.CycleLines(); } }
        public ConvexPolygon(IReadOnlyList<Point> corners) {
            this.Corners = corners;
        }
        public override string ToString() {
            return String.Join(":", this.Corners);
        }
    }
}