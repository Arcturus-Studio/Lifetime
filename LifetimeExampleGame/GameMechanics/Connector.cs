using System.Windows;
using SnipSnap.Mathematics;

namespace SnipSnap {
    public class Connector {
        public Ball Parent;
        public Ball Child;
        public Point? CutPoint;
        public LineSegment Line { get { return this.Parent.Pos.To(this.Child.Pos); } }
    }
}