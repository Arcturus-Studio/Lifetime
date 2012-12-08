using System;
using System.Windows;
using LifetimeExample.Mathematics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

[TestClass]
public class MathUtilitiesTest {
    private static readonly double Eps = 0.0001;
    private static readonly Point P00 = new Point(0, 0);
    private static readonly Point P01 = new Point(0, 1);
    private static readonly Point P10 = new Point(1, 0);
    private static readonly Point P11 = new Point(1, 1);

    private static readonly LineSegment LineUxUy = new LineSegment(P10, P01);
    private static readonly ConvexPolygon Tri0UxUy = new ConvexPolygon(new[] { P00, P10, P01 });

    [TestMethod]
    public void Normal() {
        new Vector(2, 0).Normal().AssertEquals(new Vector(1, 0));
        new Vector(-2, 0).Normal().AssertEquals(new Vector(-1, 0));
        new Vector(0, 2).Normal().AssertEquals(new Vector(0, 1));
        new Vector(0, 0).Normal().AssertEquals(new Vector(0, 0));
    }
    [TestMethod]
    public void Cross() {
        new Vector(0, 1).Cross(new Vector(0, 1)).AssertEquals(0);
        new Vector(1, 0).Cross(new Vector(0, 1)).AssertEquals(1);
        new Vector(0, 1).Cross(new Vector(1, 0)).AssertEquals(-1);
        new Vector(0, 1).Cross(new Vector(2, 0)).AssertEquals(-2);
    }
    [TestMethod]
    public void ScalarProjectOnto() {
        new Vector(0, 2).ScalarProjectOnto(new Vector(0, 3)).AssertEquals(2);
        new Vector(4, 2).ScalarProjectOnto(new Vector(0, 3)).AssertEquals(2);
    }
    [TestMethod]
    public void ProjectOnto() {
        new Vector(0, 2).ProjectOnto(new Vector(0, 3)).AssertEquals(new Vector(0, 2));
        new Vector(4, 2).ProjectOnto(new Vector(0, 3)).AssertEquals(new Vector(0, 2));
    }
    [TestMethod]
    public void PerpOnto() {
        new Vector(0, 2).PerpOnto(new Vector(0, 3)).AssertEquals(new Vector(0, 0));
        new Vector(4, 2).PerpOnto(new Vector(0, 3)).AssertEquals(new Vector(4, 0));
    }

    [TestMethod]
    public void LineSegmentProperties() {
        var line = P01.To(P10);
        line.Start.AssertEquals(P01);
        line.End.AssertEquals(P10);
        line.Delta.AssertEquals(new Vector(1, -1));
    }
    [TestMethod]
    public void ConvexPolygonProperties() {
        var poly = Tri0UxUy;
        poly.Corners.AssertSequenceEquals(P00, P10, P01);
        poly.Edges.AssertSequenceEquals(P00.To(P10), P10.To(P01), P01.To(P00));
    }

    [TestMethod]
    public void PointDistanceToPoint() {
       P00.DistanceTo(P11).AssertEquals(Math.Sqrt(2), Eps);
    }
    [TestMethod]
    public void PointDistanceToLine() {
        // on end point
        P10.DistanceTo(LineUxUy).AssertEquals(0, Eps);
        P01.DistanceTo(LineUxUy).AssertEquals(0, Eps);
        // on line
        new Point(0.5, 0.5).DistanceTo(LineUxUy).AssertEquals(0, Eps);
        // near edge
        new Point(0.7, 0.5).DistanceTo(LineUxUy).AssertEquals(0.1 * Math.Sqrt(2), Eps);
        new Point(0.6, 0.6).DistanceTo(LineUxUy).AssertEquals(0.1 * Math.Sqrt(2), Eps);
        new Point(0.4, 0.4).DistanceTo(LineUxUy).AssertEquals(0.1 * Math.Sqrt(2), Eps);
        // near end point
        new Point(2, 0).DistanceTo(LineUxUy).AssertEquals(1, Eps);
    }
    [TestMethod]
    public void PointDistanceToPolygon() {
        // on corner
        Tri0UxUy.Corners.Select(e => e.DistanceTo(Tri0UxUy)).Max().AssertEquals(0, Eps);
        // on edge
        new Point(0.5, 0.5).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        // inside
        new Point(0.2, 0.2).DistanceTo(Tri0UxUy).AssertEquals(0);
        // near corner
        new Point(2, 0).DistanceTo(Tri0UxUy).AssertEquals(1, Eps);
        // near edge
        new Point(0.6, 0.6).DistanceTo(Tri0UxUy).AssertEquals(0.1 * Math.Sqrt(2), Eps);
    }
    [TestMethod]
    public void LineDistanceToLine() {
        // crossing
        P00.To(P11).DistanceTo(P10.To(P01)).AssertEquals(0);
        P10.To(P01).DistanceTo(P00.To(P11)).AssertEquals(0);
        P00.To(P11).DistanceTo(P01.To(P10)).AssertEquals(0);

        // parallel side-by-side
        P00.To(P10).DistanceTo(P01.To(P11)).AssertEquals(1, Eps);
        P00.To(P01).DistanceTo(P10.To(P11)).AssertEquals(1, Eps);

        // parallel on same line
        var p22 = new Point(2, 2);
        P00.To(p22).DistanceTo(P11.To(new Point(3, 3))).AssertEquals(0, Eps);
        P00.To(P11).DistanceTo(p22.To(new Point(3, 3))).AssertEquals(Math.Sqrt(2), Eps);
        P00.To(P11).DistanceTo(P11.To(p22)).AssertEquals(0, Eps);
    }
    [TestMethod]
    public void LineDistanceToPolygon() {
        // along side
        Tri0UxUy.Edges.Select(e => e.DistanceTo(Tri0UxUy)).Max().AssertEquals(0, Eps);
        // in to out
        new Point(0.2, 0.2).To(new Point(1, 1)).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        // out to in
        new Point(1, 1).To(new Point(0.2, 0.2)).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        // across
        new Point(1, 1).To(new Point(-1, -1)).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        // in
        new Point(0.2, 0.2).To(new Point(0.4, 0.4)).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        // touch side
        new Point(0.5, 0.5).To(new Point(2, 1)).DistanceTo(Tri0UxUy).AssertEquals(0, Eps);
        
        // endpoint near
        new Point(0.6, 0.6).To(new Point(0.6, 0.7)).DistanceTo(Tri0UxUy).AssertEquals(0.1 * Math.Sqrt(2), Eps);
        // endpoint far
        new Point(2, 2).To(new Point(3, 4)).DistanceTo(Tri0UxUy).AssertEquals(1.5 * Math.Sqrt(2), Eps);
        // midpoint closest
        new Point(3, -1).To(new Point(-1, 3)).DistanceTo(Tri0UxUy).AssertEquals(0.5*Math.Sqrt(2), Eps);
    }
}
