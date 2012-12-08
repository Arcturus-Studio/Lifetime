using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using LifetimeExample.Mathematics;
using TwistedOak.Util;
using System.Reactive.Linq;
using LineSegment = LifetimeExample.Mathematics.LineSegment;

namespace LifetimeExample2 {
    public static class Util {
        public static double MinDistanceFromPointToLineOverTime(this LineSegment targetTrajectory, LineSegment endPoint1Trajectory, LineSegment endPoint2Trajectory) {
            var sweepOrigin = endPoint1Trajectory.Start;
            var sweepLine = new LineSegment(endPoint2Trajectory.Start, endPoint2Trajectory.End - endPoint1Trajectory.Delta);
            var targetLine = new LineSegment(targetTrajectory.Start, targetTrajectory.End - endPoint1Trajectory.Delta);

            var triangle = new ConvexPolygon(new[] { sweepOrigin, sweepLine.Start, sweepLine.End });
            return targetLine.DistanceTo(triangle);
        }
    }
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();

            GameLoop(Lifetime.Immortal);
        }
        public class Iter {
            public TimeSpan dt;
        }
        public class Ball {
            public Point LastPos;
            public Point Pos;
            public Vector Vel;
            public double Radius;
            public double Hue;
            public LifetimeSource Life;
            public int Generation;
            public TimeSpan? Death;
        }
        public class BallLine {
            public Ball Parent;
            public Ball Child;
        }
        private Color HueColor(double hue) {
            var r = (byte)Math.Floor((hue.DifMod(0, 3).Abs() - 0.5)*256).Between(0, 255);
            var g = (byte)Math.Floor((hue.DifMod(1, 3).Abs() - 0.5) * 256).Between(0, 255);
            var b = (byte)Math.Floor((hue.DifMod(2, 3).Abs() - 0.5) * 256).Between(0, 255);
            return Color.FromRgb(r, g, b);
        }
        private async void GameLoop(Lifetime gameLifetime) {
            var gameLoopActions = new PerishableCollection<Action<Iter>>();
            var balls = new PerishableCollection<Ball>();
            var lines = new PerishableCollection<BallLine>();
            var r = new Random();

            Func<TimeSpan, Lifetime> delay = duration => {
                var dt = TimeSpan.Zero;
                var life = new LifetimeSource();
                gameLoopActions.Add(
                    iter => {
                        dt += iter.dt;
                        if (dt >= duration) life.EndLifetime();
                    },
                    life.Lifetime);
                return life.Lifetime;
            };


            var curMousePos = default(Point?);
            this.MouseMove += (sender, arg) => curMousePos = arg.GetPosition(grid);
            this.MouseLeave += (sender, arg) => curMousePos = null;
            var prevMousePos = curMousePos;
            gameLoopActions.Add(iter => {
                if (!curMousePos.HasValue || !prevMousePos.HasValue) {
                    prevMousePos = curMousePos;
                    return;
                }
                var mouseTrajectory = new LineSegment(prevMousePos.Value, curMousePos.Value);
                prevMousePos = curMousePos;

                foreach (var e in lines.CurrentItems()) {
                    var endPoint1Trajectory = new LineSegment(e.Value.Parent.LastPos, e.Value.Parent.Pos);
                    var endPoint2Trajectory = new LineSegment(e.Value.Child.LastPos, e.Value.Child.Pos);
                    if (mouseTrajectory.MinDistanceFromPointToLineOverTime(endPoint1Trajectory, endPoint2Trajectory) > 1) continue;
                    e.Value.Child.Life.EndLifetime();
                }
            }, gameLifetime);

            Func<Ball, Ball> spawnBall = null;
            spawnBall = parent => {
                var lifeS = new LifetimeSource();
                parent.Life.Lifetime.WhenDead(() => delay(TimeSpan.FromMilliseconds(100 + r.NextDouble()*100)).WhenDead(lifeS.EndLifetime));
                var ball = new Ball {
                    Pos = parent.Pos,
                    Radius = 0.8 * parent.Radius,
                    Life = lifeS,
                    Generation = parent.Generation + 1,
                    Hue = parent.Hue + r.NextDouble() * 0.4
                };
                var life = ball.Life.Lifetime;
                balls.Add(ball, life);

                var omit = 0.1;
                var quarter = Math.PI / 2;
                var theta = r.NextDouble() * quarter * (1 - 2 * omit)
                          + quarter * omit
                          + r.Next(4) * quarter;
                ball.Vel = parent.Vel + 30 * new Vector(Math.Cos(theta), Math.Sin(theta));
                ball.LastPos = ball.Pos;

                // move ball around
                gameLoopActions.Add(
                    iter => {
                        var t = iter.dt.TotalSeconds;

                        // move
                        ball.LastPos = ball.Pos;
                        ball.Pos += ball.Vel * t;

                        // naive bounce back after going off the side
                        var vx = ball.Vel.X.MatchSign(-ball.Pos.X.RangeSign(0, grid.ActualWidth - ball.Radius));
                        var vy = ball.Vel.Y.MatchSign(-ball.Pos.Y.RangeSign(0, grid.ActualHeight - ball.Radius));
                        ball.Vel = new Vector(vx, vy);
                    },
                    life);

                // spawn children periodically
                if (ball.Generation < 4) {
                    var children = new PerishableCollection<Ball>();
                    var ex = new LifetimeExchanger();
                    children.AsObservable().ObserveNonPerishedCount(completeWhenSourceCompletes: true).Subscribe(
                        e => {
                            var rx = ex.StartNextAndEndPreviousLifetime();
                            if (e >= 3) return; // at most 3 children
                            gameLoopActions.Add(
                                iter => {
                                    if (r.NextDouble() > 0.01) return;
                                    var s = spawnBall(ball);
                                    lines.Add(new BallLine { Child = s, Parent = ball }, s.Life.Lifetime);
                                    children.Add(s, s.Life.Lifetime);
                                },
                                life.Min(rx));
                        },
                        life);
                }

                return ball;
            };

            // spawn a ball when there's none
            balls.AsObservable().ObserveNonPerishedCount(completeWhenSourceCompletes: true)
                 .Where(e => e == 0)
                 .Subscribe(
                     e => spawnBall(new Ball {
                         Pos = new Point(r.NextDouble()*grid.ActualWidth, r.NextDouble()*grid.ActualHeight),
                         Radius = 10,
                         Life = new LifetimeSource(),
                         Hue = r.NextDouble() * 3
                     }),
                     gameLifetime);

            Func<TimeSpan, Action<Iter, double, TimeSpan>, Lifetime> animation = (duration, animate) => {
                var dt = TimeSpan.Zero;
                var life = new LifetimeSource();
                gameLoopActions.Add(
                    iter => {
                        dt += iter.dt;
                        if (dt < duration) {
                            animate(iter, dt.TotalSeconds/duration.TotalSeconds, dt);
                        } else {
                            life.EndLifetime();
                        }
                    },
                    life.Lifetime);
                return life.Lifetime;
            };

            lines.AsObservable().Subscribe(
                e => {
                    var ball = e.Value;
                    var thickness = (e.Value.Parent.Radius + e.Value.Child.Radius)/2*0.1;
                    var line = new Line {
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = thickness,
                    };
                    grid.Children.Add(line);

                    e.Lifetime.WhenDead(() => 
                        animation(
                            TimeSpan.FromSeconds(0.8),
                            (iter, prop, dt) => {
                                line.StrokeThickness = thickness*(1 + 5*prop);
                                line.Stroke = new SolidColorBrush(Color.FromArgb((1 - prop).ProportionToByte(), 0, 0, 0));
                                line.X1 = ball.Parent.Pos.X;
                                line.Y1 = ball.Parent.Pos.Y;
                            })
                        .WhenDead(() => grid.Children.Remove(line)));
                    gameLoopActions.Add(
                        iter => {
                            line.X1 = ball.Parent.Pos.X;
                            line.Y1 = ball.Parent.Pos.Y;
                            line.X2 = ball.Child.Pos.X;
                            line.Y2 = ball.Child.Pos.Y;
                        },
                        e.Lifetime);
                },
                gameLifetime);

            // draw any living balls
            balls.AsObservable().Subscribe(
                e => {
                    var ball = e.Value;

                    var color = HueColor(ball.Hue);
                    var ellipse = new Ellipse {
                        Width = ball.Radius * 2,
                        Height = ball.Radius * 2,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Fill = new SolidColorBrush(color)
                    };
                    ellipse.MouseDown += (sender, arg) => ball.Life.EndLifetime();
                    grid.Children.Add(ellipse);
                    e.Lifetime.WhenDead(() =>
                        animation(
                            TimeSpan.FromSeconds(0.8),
                            (iter, prop, dt) => {
                                ellipse.Fill = new SolidColorBrush(Color.FromArgb((1 - prop).ProportionToByte(), color.R, color.G, color.B));
                                var radius = ball.Radius*(1 + 3*prop);
                                ellipse.Width = ellipse.Height = radius * 2;
                                ellipse.RenderTransform = new TranslateTransform(ball.Pos.X - radius, ball.Pos.Y - radius);
                            })
                        .WhenDead(() => grid.Children.Remove(ellipse)));

                    gameLoopActions.Add(
                        iter => ellipse.RenderTransform = new TranslateTransform(ball.Pos.X - ball.Radius, ball.Pos.Y - ball.Radius),
                        e.Lifetime);
                },
                gameLifetime);
                
            var lastTime = DateTime.Now;
            while (true) {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
                var dt = DateTime.Now - lastTime;
                lastTime += dt;

                foreach (var e in gameLoopActions.CurrentItems())
                    e.Value.Invoke(new Iter() { dt = dt });
            }
        }
    }
}
