using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using LifetimeExample.Mathematics;
using TwistedOak.Util;
using System.Reactive.Linq;
using LineSegment = LifetimeExample.Mathematics.LineSegment;

namespace LifetimeExample2 {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();

            var game = new Game();
            GameLoop(game);
        }
        public class Iter {
            public TimeSpan dt;
        }
        public class Game {
            public readonly PerishableCollection<Action<Iter>> LoopActions = new PerishableCollection<Action<Iter>>();
            public readonly PerishableCollection<Ball> Balls = new PerishableCollection<Ball>();
            public readonly PerishableCollection<BallLine> BallLines = new PerishableCollection<BallLine>();
            public readonly LifetimeSource LifeSource = new LifetimeSource();
            public Lifetime Life { get { return LifeSource.Lifetime; }}
            public readonly Random Rng = new Random();

            public async void Loop() {
                var clock = new Stopwatch();
                clock.Start();
                var lastTime = clock.Elapsed;
                while (true) {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                    if (Life.IsDead) break;
                    var dt = clock.Elapsed - lastTime;
                    lastTime += dt;
                    var smoothedDt = dt.Clamp(TimeSpan.Zero, TimeSpan.FromSeconds(1));

                    foreach (var e in LoopActions.CurrentItems())
                        e.Value.Invoke(new Iter { dt = smoothedDt });
                }
            }

            public delegate void AnimationCallback(Iter iter, double proportionCompleted, TimeSpan elapsed);
            public Lifetime Delay(TimeSpan duration) {
                var remaining = duration;
                var life = new LifetimeSource();
                LoopActions.Add(
                    iter => {
                        remaining -= iter.dt;
                        if (remaining < TimeSpan.Zero) life.EndLifetime();
                    },
                    life.Lifetime);
                return life.Lifetime;
            }
            public Lifetime AnimateWith(TimeSpan duration, AnimationCallback callback) {
                var remaining = duration;
                var life = new LifetimeSource();
                LoopActions.Add(
                    iter => {
                        remaining -= iter.dt;
                        if (remaining >= TimeSpan.Zero) {
                            callback(iter, remaining.TotalSeconds / duration.TotalSeconds, remaining);
                        } else {
                            life.EndLifetime();
                        }
                    },
                    life.Lifetime);
                return life.Lifetime;
            }
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
        private void GameLoop(Game game) {
            var curMousePos = default(Point?);
            this.MouseMove += (sender, arg) => curMousePos = arg.GetPosition(grid);
            this.MouseLeave += (sender, arg) => curMousePos = null;
            var prevMousePos = curMousePos;
            game.LoopActions.Add(iter => {
                if (!curMousePos.HasValue || !prevMousePos.HasValue) {
                    prevMousePos = curMousePos;
                    return;
                }
                var mouseTrajectory = new LineSegment(prevMousePos.Value, curMousePos.Value);
                prevMousePos = curMousePos;

                foreach (var e in game.BallLines.CurrentItems()) {
                    var endPoint1Trajectory = new LineSegment(e.Value.Parent.LastPos, e.Value.Parent.Pos);
                    var endPoint2Trajectory = new LineSegment(e.Value.Child.LastPos, e.Value.Child.Pos);
                    if (mouseTrajectory.ApproximateMinDistanceFromPointToLineOverTime(endPoint1Trajectory, endPoint2Trajectory) > 1) continue;
                    e.Value.Child.Life.EndLifetime();
                }
            }, game.Life);

            Func<Ball, Ball> spawnBall = null;
            spawnBall = parent => {
                var lifeS = new LifetimeSource();
                parent.Life.Lifetime.WhenDead(() => game.Delay(TimeSpan.FromMilliseconds(100 + game.Rng.NextDouble() * 100)).WhenDead(lifeS.EndLifetime));
                var ball = new Ball {
                    Pos = parent.Pos,
                    Radius = 0.8 * parent.Radius,
                    Life = lifeS,
                    Generation = parent.Generation + 1,
                    Hue = parent.Hue + game.Rng.NextDouble() * 0.4
                };
                var life = ball.Life.Lifetime;
                game.Balls.Add(ball, life);

                var omit = 0.1;
                var quarter = Math.PI / 2;
                var theta = game.Rng.NextDouble() * quarter * (1 - 2 * omit)
                          + quarter * omit
                          + game.Rng.Next(4) * quarter;
                ball.Vel = parent.Vel + 30 * new Vector(Math.Cos(theta), Math.Sin(theta));
                ball.LastPos = ball.Pos;

                // move ball around
                game.LoopActions.Add(
                    iter => {
                        var t = iter.dt.TotalSeconds;

                        // move
                        ball.LastPos = ball.Pos;
                        ball.Pos += ball.Vel * t;

                        // naive bounce back after going off the side
                        var vx = ball.Vel.X.RangeBounceVelocity(ball.Pos.X, 0, (grid.ActualWidth - ball.Radius).Max(0));
                        var vy = ball.Vel.Y.RangeBounceVelocity(ball.Pos.Y, 0, (grid.ActualHeight - ball.Radius).Max(0));
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
                            game.LoopActions.Add(
                                iter => {
                                    if (game.Rng.NextDouble() > 0.01) return;
                                    var s = spawnBall(ball);
                                    game.BallLines.Add(new BallLine { Child = s, Parent = ball }, s.Life.Lifetime);
                                    children.Add(s, s.Life.Lifetime);
                                },
                                life.Min(rx));
                        },
                        life);
                }

                return ball;
            };

            // spawn a ball when there's none
            game.Balls.AsObservable().ObserveNonPerishedCount(completeWhenSourceCompletes: true)
                 .Where(e => e == 0)
                 .Subscribe(
                     e => spawnBall(new Ball {
                         Pos = new Point(game.Rng.NextDouble() * grid.ActualWidth, game.Rng.NextDouble() * grid.ActualHeight),
                         Radius = 10,
                         Life = new LifetimeSource(),
                         Hue = game.Rng.NextDouble() * 3
                     }),
                     game.Life);


            game.BallLines.AsObservable().Subscribe(
                e => {
                    var ball = e.Value;
                    var thickness = (e.Value.Parent.Radius + e.Value.Child.Radius) / 2 * 0.1;
                    var line = new Line {
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = thickness,
                    };
                    grid.Children.Add(line);

                    e.Lifetime.WhenDead(() =>
                        game.AnimateWith(
                            TimeSpan.FromSeconds(0.8),
                            (iter, prop, dt) => {
                                line.StrokeThickness = thickness * (1 + 5 * prop);
                                line.Stroke = new SolidColorBrush(Color.FromArgb((1 - prop).ProportionToByte(), 0, 0, 0));
                                line.X1 = ball.Parent.Pos.X;
                                line.Y1 = ball.Parent.Pos.Y;
                            })
                        .WhenDead(() => grid.Children.Remove(line)));
                    game.LoopActions.Add(
                        iter => {
                            line.X1 = ball.Parent.Pos.X;
                            line.Y1 = ball.Parent.Pos.Y;
                            line.X2 = ball.Child.Pos.X;
                            line.Y2 = ball.Child.Pos.Y;
                        },
                        e.Lifetime);
                },
                game.Life);

            SetupDrawBalls(game);

            game.Loop();
        }
        private void SetupDrawBalls(Game game) {
            var DeathAnimationDuration = 800.Milliseconds();
            var DeathFinalRadiusFactor = 4;

            game.Balls.AsObservable().Subscribe(
                e => {
                    var ball = e.Value;
                    var color = ball.Hue.HueToApproximateColor(period: 3);

                    var ellipse = new Ellipse {
                        Width = ball.Radius * 2,
                        Height = ball.Radius * 2,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Fill = new SolidColorBrush(color)
                    };
                    grid.Children.Add(ellipse);

                    // move ellipse control to position during each game loop, until ball is dead
                    game.LoopActions.Add(
                        iter => ellipse.RenderTransform = new TranslateTransform(ball.Pos.X - ball.Radius, ball.Pos.Y - ball.Radius),
                        e.Lifetime);

                    // once ball is dead, expand and fade out the ellipse
                    e.Lifetime.WhenDead(() =>
                        game.AnimateWith(
                            DeathAnimationDuration,
                            (iter, prop, dt) => {
                                // fade out
                                ellipse.Fill = new SolidColorBrush(color.LerpToTransparent(prop));
                                // expand
                                var radius = ball.Radius * 1.LerpTo(DeathFinalRadiusFactor, prop);
                                ellipse.Width = ellipse.Height = radius * 2;
                                ellipse.RenderTransform = new TranslateTransform(ball.Pos.X - radius, ball.Pos.Y - radius);
                            })
                        // once the death animation is done, discard the ellipse control
                        .WhenDead(() => grid.Children.Remove(ellipse)));
                },
                game.Life);
        }
    }
}
