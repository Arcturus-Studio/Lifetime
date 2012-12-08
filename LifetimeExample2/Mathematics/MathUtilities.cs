using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LifetimeExample.Mathematics {
    public static class MathUtilities {
        public static double Sign(this double d) {
            return Math.Sign(d);
        }
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
        public static byte ProportionToByte(this double proportion) {
            return (byte)Math.Floor(proportion*256).Between(0, 255);
        }
        public static IEnumerable<LineSegment> CycleLines(this IEnumerable<Point> items) {
            return items.CyclePairs().Select(e => new LineSegment(e.Item1, e.Item2));
        }
        public static double Between(this double p, double min, double max) {
            if (max < min) throw new ArgumentException();
            if (p < min) return min;
            if (p > max) return max;
            return p;
        }
        public static int RangeSign(this double p, double min, double max) {
            if (p < min) return -1;
            if (p > max) return +1;
            return 0;
        }
        public static double MatchSign(this double p, double s) {
            if (s == 0) return p;
            return Math.Abs(p) * Math.Sign(s);
        }
        public static double DifMod(this double v1, double v2, double m) {
            v1 -= v2;
            v1 %= m;
            if (v1 < -m / 2) v1 += m;
            if (v1 >= m / 2) v1 -= m;
            return v1;
        }
        public static double Abs(this double v) {
            return Math.Abs(v);
        }
        public static double Max(this double v1, double v2) {
            return Math.Max(v1, v2);
        }
    }
}
