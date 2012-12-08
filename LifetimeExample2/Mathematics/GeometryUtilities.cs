using System.Linq;
using System.Windows;

namespace LifetimeExample.Mathematics {
    public static class GeometryUtilities {
        public static Vector Normal(this Vector vector) {
            vector.Normalize();
            return vector;
        }
        public static double Cross(this Vector v1, Vector v2) {
            return v1.X * v2.Y - v1.Y * v2.X;
        }
        public static double ScalarProjectOnto(this Vector vector, Vector direction) {
            return vector * direction.Normal();
        }
        public static Vector ProjectOnto(this Vector vector, Vector direction) {
            return ((vector*direction) * direction) / direction.LengthSquared;
        }
        public static Vector PerpOnto(this Vector vector, Vector direction) {
            return vector - vector.ProjectOnto(direction);
        }
        public static LineSegment To(this Point p1, Point p2) {
            return new LineSegment(p1, p2);
        }

        public static double DistanceTo(this Point point, Point other) {
            return (point - other).Length;
        }
        public static double DistanceTo(this Point point, LineSegment line) {
            var offset = point - line.Start;
            var s = offset.ScalarProjectOnto(line.Delta)/line.Delta.Length;
            if (s < 0) return point.DistanceTo(line.Start);
            if (s > 1) return point.DistanceTo(line.End);
            return offset.PerpOnto(line.Delta).Length;
        }
        public static double DistanceTo(this LineSegment line, LineSegment line2) {
            // do they intersect?
            var c = line.Delta.Cross(line2.Delta);
            var d = line2.Start - line.Start;
            var t1 = d.Cross(line2.Delta)/c;
            var t2 = d.Cross(line.Delta)/c;
            if (0 <= t1 && t1 <= 1 && 0 <= t2 && t2 <= 1)
                return 0;

            // otherwise one of their endpoints is as close to the other as any other point
            return new[] {
                line.End.DistanceTo(line2),
                line.Start.DistanceTo(line2),
                line2.End.DistanceTo(line),
                line2.Start.DistanceTo(line)
            }.Min();
        }
        public static double DistanceTo(this Point point, ConvexPolygon polygon) {
            // inside?
            if (polygon.Edges.Select(e => (int)e.Delta.Cross(point - e.Start).Sign())
                             .Where(e => e != 0)
                             .Distinct()
                             .Count() < 2) 
                return 0;
            // when outside, one of the edges is closest
            return polygon.Edges.Select(e => point.DistanceTo(e)).Min();
        }
        public static double DistanceTo(this LineSegment line, Point point) {
            return point.DistanceTo(line);
        }
        public static double DistanceTo(this ConvexPolygon polygon, Point point) {
            return point.DistanceTo(polygon);
        }
        public static double DistanceTo(this LineSegment line, ConvexPolygon polygon) {
            return new[] {
                // use distance 0 if an endpoint is inside the polygon
                line.Start.DistanceTo(polygon),
                // otherwise use how close to the sides the line gets
                polygon.Edges.Select(e => line.DistanceTo(e)).Min()
            }.Min();
        }
        public static double DistanceTo(this ConvexPolygon polygon, LineSegment line) {
            return line.DistanceTo(polygon);
        }
    }
}
