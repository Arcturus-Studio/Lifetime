using System;
using System.Diagnostics;
using System.Linq;
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
            public readonly PerishableCollection<BallLine> Connectors = new PerishableCollection<BallLine>();
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
                            callback(iter, 1 - remaining.TotalSeconds / duration.TotalSeconds, remaining);
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
            public LineSegment Line { get { return Parent.Pos.To(Child.Pos); } }
        }
        private void GameLoop(Game game) {
            SetupMouseCutter(game);
            
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
                                    game.Connectors.Add(new BallLine { Child = s, Parent = ball }, s.Life.Lifetime);
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

            SetupDrawConnectors(game);
            SetupDrawBalls(game);

            game.Loop();
        }

        private Lifetime AnimateSpinningRectangleExplosion(Game game, Func<Point> position, Color color, TimeSpan duration, double rotationsPerSecond, double finalRadius) {
            var rect = new Rectangle {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            grid.Children.Add(rect);
            var life = game.AnimateWith(
                duration,
                (ix, prop, ellapsed) => {
                    var r = 0.LerpTo(finalRadius, prop);
                    var pt = position();
                    rect.Width = rect.Height = r*2;
                    rect.RenderTransform = new TransformGroup {
                        Children = new TransformCollection {
                            new RotateTransform(rotationsPerSecond*ellapsed.TotalSeconds*360),
                            new TranslateTransform(pt.X - r, pt.Y - r)
                        }
                    };
                    rect.Fill = new SolidColorBrush(color.LerpToTransparent(prop));
                });
            life.WhenDead(() => grid.Children.Remove(rect));
            return life;
        }
        private void SetupMouseCutter(Game game) {
            var CutTolerance = 1.0;

            // track current mouse position
            var liveMousePos = default(Point?);
            this.MouseMove += (sender, arg) => liveMousePos = arg.GetPosition(grid);
            this.MouseLeave += (sender, arg) => liveMousePos = null;

            var lastUsedMousePos = liveMousePos;
            game.LoopActions.Add(
                iter => {
                    // get a path between last and current mouse positions, if any
                    var prev = lastUsedMousePos;
                    lastUsedMousePos = liveMousePos;
                    if (!liveMousePos.HasValue || !prev.HasValue) return;
                    var cutPath = new LineSegment(prev.Value, liveMousePos.Value);
                    var cur = liveMousePos.Value;

                    // cut any connectors that crossed the cut path
                    foreach (var cutConnector in from connector in game.Connectors.CurrentItems()
                                                 let endPath1 = new LineSegment(connector.Value.Parent.LastPos, connector.Value.Parent.Pos)
                                                 let endPath2 = new LineSegment(connector.Value.Child.LastPos, connector.Value.Child.Pos)
                                                 where cutPath.ApproximateMinDistanceFromPointToLineOverTime(endPath1, endPath2) <= CutTolerance
                                                 select connector) {
                        cutConnector.Value.Child.Life.EndLifetime();

                        AnimateSpinningRectangleExplosion(
                            game,
                            () => cur.ClosestPointOn(cutConnector.Value.Line),
                            Colors.Red,
                            300.Milliseconds(),
                            20,
                            20);
                    }
                },
                game.Life);
        }

        public static void SetPosition(Line lineControl, LineSegment position) {
            lineControl.X1 = position.Start.X;
            lineControl.Y1 = position.Start.Y;
            lineControl.X2 = position.End.X;
            lineControl.Y2 = position.End.Y;
        }
        public static void SetPosition(Ellipse ellipseControl, Point center, double radius) {
            ellipseControl.Width = ellipseControl.Height = radius * 2;
            ellipseControl.RenderTransform = new TranslateTransform(center.X - radius, center.Y - radius);
        }

        private void SetupDrawConnectors(Game game) {
            var DeathFinalThicknessFactor = 6;
            game.Connectors.AsObservable().Subscribe(
                e => {
                    var thickness = e.Value.Child.Radius * 0.1;

                    // create a line control to visually represent the line
                    var lineControl = new Line {
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = thickness,
                    };
                    grid.Children.Add(lineControl);
                    
                    // move line control to position during each game loop, until the connector is dead
                    game.LoopActions.Add(
                        iter => SetPosition(lineControl, e.Value.Line),
                        e.Lifetime);

                    // once connector is dead, expand and fade out the line control
                    e.Lifetime.WhenDead(() => {
                        AnimateSpinningRectangleExplosion(game, () => e.Value.Line.Mid, Colors.Orange, 200.Milliseconds(), 30, 10);
                        game.AnimateWith(
                            TimeSpan.FromSeconds(0.8),
                            (iter, prop, dt) => {
                                lineControl.StrokeThickness = thickness*1.LerpTo(DeathFinalThicknessFactor, prop);
                                lineControl.Stroke = new SolidColorBrush(Colors.Black.LerpToTransparent(prop));
                                SetPosition(lineControl, e.Value.Line);
                            })
                            // once the death animation is done, discard the line control
                            .WhenDead(() => grid.Children.Remove(lineControl));
                    });
                },
                game.Life);
            
        }
        private void SetupDrawBalls(Game game) {
            var DeathAnimationDuration = 800.Milliseconds();
            var DeathFinalRadiusFactor = 3;

            game.Balls.AsObservable().Subscribe(
                e => {
                    var ball = e.Value;
                    var color = ball.Hue.HueToApproximateColor(period: 3);

                    // create an ellipse control to visually represent the ball
                    var ellipse = new Ellipse {
                        Width = ball.Radius * 2,
                        Height = ball.Radius * 2,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Fill = new SolidColorBrush(color)
                    };
                    grid.Children.Add(ellipse);

                    if (ball.Generation == 1) {
                        ellipse.StrokeThickness = 3;
                        ellipse.Stroke = new SolidColorBrush(Colors.Black);
                    }

                    // move ellipse control to position during each game loop, until ball is dead
                    game.LoopActions.Add(
                        iter => SetPosition(ellipse, ball.Pos, ball.Radius),
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
                                SetPosition(ellipse, ball.Pos, radius);
                            })
                        // once the death animation is done, discard the ellipse control
                        .WhenDead(() => grid.Children.Remove(ellipse)));
                },
                game.Life);
        }
    }
}
