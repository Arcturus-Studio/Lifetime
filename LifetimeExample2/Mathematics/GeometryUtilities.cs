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

        public static bool Intersects(this LineSegment line1, LineSegment line2) {
            var d = line2.Start - line1.Start;
            var c = line1.Delta.Cross(line2.Delta);
            var t1 = d.Cross(line2.Delta) / c;
            var t2 = d.Cross(line1.Delta) / c;
            return 0 <= t1 && t1 <= 1
                && 0 <= t2 && t2 <= 1;
        }
        public static bool Intersects(this Point point, ConvexPolygon polygon) {
            return polygon.Edges
                .Select(e => (int)e.Delta.Cross(point - e.Start).Sign())
                .Where(e => e != 0)
                .Distinct()
                .Count() < 2;
        }
        public static bool Intersects(this LineSegment line, ConvexPolygon polygon) {
            if (line.Start.Intersects(polygon)) return true;
            if (line.End.Intersects(polygon)) return true;
            return polygon.Edges.Any(e => line.Intersects(e));
        }
        public static bool Intersects(this ConvexPolygon polygon, Point point) {
            return point.Intersects(polygon);
        }
        public static bool Intersects(this ConvexPolygon polygon, LineSegment line) {
            return line.Intersects(polygon);
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
        public static double DistanceTo(this LineSegment line, LineSegment other) {
            if (line.Intersects(other)) return 0;
            return new[] {
                line.End.DistanceTo(other),
                line.Start.DistanceTo(other),
                other.End.DistanceTo(line),
                other.Start.DistanceTo(line)
            }.Min();
        }
        public static double DistanceTo(this Point point, ConvexPolygon polygon) {
            if (Intersects(point, polygon)) return 0;
            return polygon.Edges.Select(e => point.DistanceTo(e)).Min();
        }
        public static double DistanceTo(this LineSegment line, Point point) {
            return point.DistanceTo(line);
        }
        public static double DistanceTo(this ConvexPolygon polygon, Point point) {
            return point.DistanceTo(polygon);
        }
        public static double DistanceTo(this LineSegment line, ConvexPolygon polygon) {
            if (line.Intersects(polygon)) return 0;
            return polygon.Edges.Select(e => line.DistanceTo(e)).Min();
        }
        public static double DistanceTo(this ConvexPolygon polygon, LineSegment line) {
            return line.DistanceTo(polygon);
        }
    }
}
