﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using TwistedOak.Util;

namespace SnipSnap.Mathematics {
    ///<summary>Utility methods for working with numbers and related concepts.</summary>
    public static class MathUtilities {
        ///<summary>The sign (+1, -1, or 0) of a number.</summary>
        public static int Sign(this double value) {
            return Math.Sign(value);
        }
        ///<summary>The non-negative absolute magnitude of a number.</summary>
        public static double Abs(this double value) {
            return Math.Abs(value);
        }
        ///<summary>The larger of two values.</summary>
        public static T Max<T>(this T value1, T value2) where T : IComparable<T> {
            return value1.CompareTo(value2) >= 0 ? value1 : value2;
        }
        ///<summary>The lesser of two values.</summary>
        public static T Min<T>(this T value1, T value2) where T : IComparable<T> {
            return value1.CompareTo(value2) <= 0 ? value1 : value2;
        }
        ///<summary>Clamps a value to be not-less-than a minimum and not-larger-than a maximum.</summary>
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T> {
            if (max.CompareTo(min) < 0) throw new ArgumentOutOfRangeException("max", "max < min");
            return value.Max(min).Min(max);
        }
        ///<summary>Whether a value is less than (-1), contained in (0), or greater than (1) a contiguous range defined by a minimum and a maximum.</summary>
        public static int RangeSign<T>(this T value, T min, T max) where T : IComparable<T> {
            if (max.CompareTo(min) < 0) throw new ArgumentOutOfRangeException("max", "max < min");
            if (value.CompareTo(min) < 0) return -1;
            if (value.CompareTo(max) > 0) return +1;
            return 0;
        }

        ///<summary>The smallest possible non-negative remainder from whole-dividing a value by a divisor.</summary>
        public static double ProperMod(this double value, double divisor) {
            var r = value % divisor;
            if (r < 0) r += divisor;
            return r;
        }
        ///<summary>The smallest possible absolute (positive or negative) delta to go from value2 to value1 (modulo the given divisor).</summary>
        public static double SignedModularDifference(this double value1, double value2, double divisor) {
            var dif = (value1 - value2).ProperMod(divisor);
            if (dif >= divisor / 2) dif -= divisor;
            return dif;
        }
        ///<summary>Flips the resulting velocity to face towards the range when the position is out of range.</summary>
        public static double RangeBounceVelocity(this double velocity, double position, double minPosition, double maxPosition) {
            var r = position.RangeSign(minPosition, maxPosition);
            if (r == 0) return velocity;
            return velocity.Abs() * -r;
        }
        ///<summary>Maps the continuous range [0, 1) linearly onto the discrete range [0, 256), clamping input values outside [0, 1) to be in range.</summary>
        public static byte ProportionToByte(this double proportion) {
            return (byte)Math.Floor(proportion * 256).Clamp(0, 255);
        }

        ///<summary>Enumerates the contiguous pairs of items in a sequence as if it were a cycle, meaning (last, first) is one of the pairs).</summary>
        public static IEnumerable<Tuple<T, T>> CyclePairs<T>(this IEnumerable<T> items) {
            using (var e = items.GetEnumerator()) {
                if (!e.MoveNext()) yield break;
                var first = e.Current;
                var prev = e.Current;
                if (!e.MoveNext()) yield break;
                do {
                    yield return Tuple.Create(prev, e.Current);
                    prev = e.Current;
                } while (e.MoveNext());
                yield return Tuple.Create(prev, first);
            }
        }
        ///<summary>Enumerates the lines between consecutive points, including the line between the last and first point.</summary>
        public static IEnumerable<LineSegment> CycleLines(this IEnumerable<Point> items) {
            return items.CyclePairs().Select(e => new LineSegment(e.Item1, e.Item2));
        }
        ///<summary>Enumerates the integers in [0, count) in increasing order from 0 to count-1.</summary>
        public static IEnumerable<int> Range(this int count) {
            return Enumerable.Range(0, count);
        } 

        /// <summary>
        /// The approximated closest that a point comes to a line over time.
        /// The approximation may be far too low, but not too high.
        /// </summary>
        public static double ApproximateMinDistanceFromPointToLineOverTime(this LineSegment targetTrajectory, LineSegment endPoint1Trajectory, LineSegment endPoint2Trajectory) {
            var sweepOrigin = endPoint1Trajectory.Start;
            var sweepLine = new LineSegment(endPoint2Trajectory.Start, endPoint2Trajectory.End - endPoint1Trajectory.Delta);
            var targetLine = new LineSegment(targetTrajectory.Start, targetTrajectory.End - endPoint1Trajectory.Delta);

            var triangle = new ConvexPolygon(new[] { sweepOrigin, sweepLine.Start, sweepLine.End });
            return targetLine.DistanceTo(triangle);
        }
        ///<summary>A color with a hue based on the given value, cycling around the color wheel with the given period.</summary>
        public static Color HueToApproximateColor(this double hue, double period) {
            var rgb = 3.Range().Select(i => 
                (1 - period/2 + hue.SignedModularDifference(i*period/3, period).Abs()).ProportionToByte()
            ).ToArray();
            return Color.FromRgb(rgb[0], rgb[1], rgb[2]);
        }

        public static TimeSpan Seconds(this double totalSeconds) {
            return TimeSpan.FromSeconds(totalSeconds);
        }
        public static TimeSpan Seconds(this int totalSeconds) {
            return TimeSpan.FromSeconds(totalSeconds);
        }
        public static TimeSpan Milliseconds(this double totalMilliSeconds) {
            return TimeSpan.FromMilliseconds(totalMilliSeconds);
        }
        public static TimeSpan Milliseconds(this int totalMilliSeconds) {
            return TimeSpan.FromMilliseconds(totalMilliSeconds);
        }
        public static double LerpTo(this double valueAt0, double valueAt1, double proportion) {
            return valueAt1 * proportion + valueAt0 * (1 - proportion);
        }
        public static double LerpTo(this int valueAt0, double valueAt1, double proportion) {
            return ((double)valueAt0).LerpTo(valueAt1, proportion);
        }
        public static Byte LerpTo(this byte valueAt0, byte valueAt1, double proportion) {
            return (byte)Math.Round(((double)valueAt0).LerpTo(valueAt1, proportion).Clamp(0, 255));
        }
        public static Color LerpTo(this Color valueAt0, Color valueAt1, double proportion) {
            return Color.FromArgb(
                valueAt0.A.LerpTo(valueAt1.A, proportion),
                valueAt0.R.LerpTo(valueAt1.R, proportion),
                valueAt0.G.LerpTo(valueAt1.G, proportion),
                valueAt0.B.LerpTo(valueAt1.B, proportion));
        }
        public static Color LerpToTransparent(this Color color, double proportion) {
            return color.LerpTo(Color.FromArgb(0, color.R, color.G, color.B) , proportion);
        }

        public static Lifetime WhenAfterLife(this Lifetime lifetime, Func<Lifetime> action) {
            var r = new LifetimeSource();
            lifetime.WhenDead(() => action().WhenDead(r.EndLifetime));
            return r.Lifetime;
        }
        public static Point LerpAcross(this LineSegment line, double proportion) {
            return line.Start + line.Delta*proportion;
        }
    }
}
