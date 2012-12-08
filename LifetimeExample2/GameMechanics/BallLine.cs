using System.Windows;
using LifetimeExample.Mathematics;

namespace LifetimeExample2 {
    public class BallLine {
        public Ball Parent;
        public Ball Child;
        public Point? CutPoint;
        public LineSegment Line { get { return this.Parent.Pos.To(this.Child.Pos); } }
    }
}