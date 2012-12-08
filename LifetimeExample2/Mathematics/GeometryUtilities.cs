using System.Linq;
using System.Windows;

namespace LifetimeExample.Mathematics {
    ///<summary>Contains utility methods for working with geometric types (points, vectors, line segments and convex polygons).</summary>
    ///<remarks>Attempts to be simple and correct.</remarks>
    public static class GeometryUtilities {
        ///<summary>Returns the unit vector pointing in the same direction as the vectory, or else the zero vector.</summary>
        public static Vector Normal(this Vector vector) {
            vector.Normalize();
            if (double.IsNaN(vector.X)) return default(Vector);
            return vector;
        }
        ///<summary>Returns the cross product of two vectors in 2d.</summary>
        public static double Cross(this Vector vector1, Vector vector2) {
            return vector1.X * vector2.Y - vector1.Y * vector2.X;
        }
        ///<summary>Returns the length along a given direction that a vector travels.</summary>
        public static double ScalarProjectOnto(this Vector vector, Vector direction) {
            return vector * direction.Normal();
        }
        ///<summary>Returns the displacement along a given direction that a vector travels, ignore perpendicular components.</summary>
        public static Vector ProjectOnto(this Vector vector, Vector direction) {
            return ((vector*direction) * direction) / direction.LengthSquared;
        }
        ///<summary>Returns the perpendicular component of a vector's displacement, with respect to a given direction.</summary>
        public static Vector PerpOnto(this Vector vector, Vector direction) {
            return vector - vector.ProjectOnto(direction);
        }

        ///<summary>Creates a line segment based on the given end points.</summary>
        public static LineSegment To(this Point endPoint1, Point endPoint2) {
            return new LineSegment(endPoint1, endPoint2);
        }

        ///<summary>Determines the euclidean distance between two points.</summary>
        public static double DistanceTo(this Point point, Point other) {
            return (point - other).Length;
        }
        ///<summary>Determines the minimum euclidean distance between a point and a line segment.</summary>
        public static double DistanceTo(this Point point, LineSegment line) {
            var offset = point - line.Start;
            var s = offset.ScalarProjectOnto(line.Delta)/line.Delta.Length;
            if (s < 0) return point.DistanceTo(line.Start);
            if (s > 1) return point.DistanceTo(line.End);
            return offset.PerpOnto(line.Delta).Length;
        }
        ///<summary>Determines the minimum euclidean distance between two line segments.</summary>
        public static double DistanceTo(this LineSegment line, LineSegment line2) {
            // do they intersect?
            var c = line.Delta.Cross(line2.Delta);
            var d = line2.Start - line.Start;
            var t1 = d.Cross(line2.Delta)/c;
            var t2 = d.Cross(line.Delta)/c;
            if (0 <= t1 && t1 <= 1 && 0 <= t2 && t2 <= 1)
                return 0;

            // otherwise one of their endpoints is part of a min-distance point pair
            return new[] {
                line.End.DistanceTo(line2),
                line.Start.DistanceTo(line2),
                line2.End.DistanceTo(line),
                line2.Start.DistanceTo(line)
            }.Min();
        }
        ///<summary>Determines the minimum euclidean distance between a point and a convex polygon.</summary>
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
        ///<summary>Determines the minimum euclidean distance between a convex polygon and a line segment.</summary>
        public static double DistanceTo(this LineSegment line, ConvexPolygon polygon) {
            return new[] {
                // minimum distance from the perimeter of the poylong
                polygon.Edges.Select(e => line.DistanceTo(e)).Min(),
                // if the line is entirely inside, it is distance 0 despite not being near the perimeter, so check for that
                line.Start.DistanceTo(polygon),
            }.Min();
        }

        ///<summary>Determines the minimum euclidean distance between a point and a line segment.</summary>
        public static double DistanceTo(this LineSegment line, Point point) {
            return point.DistanceTo(line);
        }
        ///<summary>Determines the minimum euclidean distance between a point and a convex polygon.</summary>
        public static double DistanceTo(this ConvexPolygon polygon, Point point) {
            return point.DistanceTo(polygon);
        }
        ///<summary>Determines the minimum euclidean distance between a convex polygon and a line segment.</summary>
        public static double DistanceTo(this ConvexPolygon polygon, LineSegment line) {
            return line.DistanceTo(polygon);
        }
    }
}
