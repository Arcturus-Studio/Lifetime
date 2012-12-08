using System.Windows;
using LifetimeExample.Mathematics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1 {
    private static readonly Point P00 = new Point(0, 0);
    private static readonly Point P01 = new Point(0, 1);
    private static readonly Point P10 = new Point(1, 0);
    private static readonly Point P11 = new Point(1, 1);
    [TestMethod]
    public void LineLineIntersection() {
        P00.To(P10).Intersects(P01.To(P11)).AssertIsFalse();
        P00.To(P01).Intersects(P10.To(P11)).AssertIsFalse();
        
        P00.To(P11).Intersects(P10.To(P01)).AssertIsTrue();
        P10.To(P01).Intersects(P00.To(P11)).AssertIsTrue();
        P00.To(P11).Intersects(P01.To(P10)).AssertIsTrue();
    }
    //public static bool Intersects(this Point point, ConvexPolygon polygon) {
    //    return polygon.Edges
    //        .Select(e => (int)e.Delta.Cross(point - e.Start).Sign())
    //        .Where(e => e != 0)
    //        .Distinct()
    //        .Count() < 2;
    //}
    //public static bool Intersects(this LineSegment line, ConvexPolygon polygon) {
    //    if (line.Start.Intersects(polygon)) return true;
    //    if (line.End.Intersects(polygon)) return true;
    //    return polygon.Edges.Any(e => line.Intersects(e));
    //}
    //public static bool Intersects(this ConvexPolygon polygon, Point point) {
    //    return point.Intersects(polygon);
    //}
    //public static bool Intersects(this ConvexPolygon polygon, LineSegment line) {
    //    return line.Intersects(polygon);
    //}

    //public static double DistanceTo(this Point point, Point other) {
    //    return (point - other).Length;
    //}
    //public static double DistanceTo(this Point point, LineSegment line) {
    //    var offset = point - line.Start;
    //    var s = offset.ScalarProjectOnto(line.Delta) / line.Delta.Length;
    //    if (s < 0) return point.DistanceTo(line.Start);
    //    if (s > 1) return point.DistanceTo(line.End);
    //    return offset.PerpOnto(line.Delta).Length;
    //}
    //public static double DistanceTo(this LineSegment line, LineSegment other) {
    //    if (line.Intersects(other)) return 0;
    //    return new[] {
    //            line.End.DistanceTo(other),
    //            line.Start.DistanceTo(other),
    //            other.End.DistanceTo(line),
    //            other.Start.DistanceTo(line)
    //        }.Min();
    //}
    //public static double DistanceTo(this Point point, ConvexPolygon polygon) {
    //    if (Intersects(point, polygon)) return 0;
    //    return polygon.Edges.Select(e => point.DistanceTo(e)).Min();
    //}
    //public static double DistanceTo(this LineSegment line, Point point) {
    //    return point.DistanceTo(line);
    //}
    //public static double DistanceTo(this ConvexPolygon polygon, Point point) {
    //    return point.DistanceTo(polygon);
    //}
    //public static double DistanceTo(this LineSegment line, ConvexPolygon polygon) {
    //    if (line.Intersects(polygon)) return 0;
    //    return polygon.Edges.Select(e => line.DistanceTo(e)).Min();
    //}
    //public static double DistanceTo(this ConvexPolygon polygon, LineSegment line) {
    //    return line.DistanceTo(polygon);
    //}
}
