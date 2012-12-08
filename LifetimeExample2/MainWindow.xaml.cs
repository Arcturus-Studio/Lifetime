using System;
using System.Windows;
using System.Windows.Media;
using LifetimeExample.Mathematics;
using TwistedOak.Util;
using System.Reactive.Linq;

namespace LifetimeExample2 {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            this.Loaded += (sender, arg) => {
                var controls = new PerishableCollection<UIElement>();
                var game = new Game();
                controls.AsObservable().Subscribe(
                    e => {
                        canvas.Children.Add(e.Value);
                        e.Lifetime.WhenDead(() => canvas.Children.Remove(e.Value));
                    },
                    game.Life);
                GameLoop(game, controls);
            };
        }
        private void GameLoop(Game game, PerishableCollection<UIElement> controls) {
            game.SetupMoveAndBounceBalls(() => new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));
            
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

            game.SetupDrawBallsAsControls(
                controls, 
                deathFadeOutDuration: 800.Milliseconds(), 
                deathFinalRadiusFactor: 3);

            game.SetupDrawConnectorsAsControls(
                controls,
                deathFadeDuration: 800.Milliseconds(),
                deathFinalThicknessFactor: 6,
                propagateBangColor: Colors.Black,
                propagateBangDuration: 400.Milliseconds(),
                propagateBangRotationsPerSecond: 3,
                propagateBangMaxRadius: 5,
                cutBangColor: Colors.Black,
                cutBangDuration: 400.Milliseconds(),
                cutBangRotationsPerSecond: 2,
                cutBangMaxRadius: 15);

            var liveMousePos = default(Point?);
            this.MouseMove += (sender, arg) => liveMousePos = arg.GetPosition(canvas);
            this.MouseLeave += (sender, arg) => liveMousePos = null;
            game.SetupMouseCutter(
                controls,
                () => liveMousePos,
                cutTolerance: 1);


            foreach (var repeat in 3.Range()) {
                spawnBall(new Ball {
                    Pos = new Point(game.Rng.NextDouble()*canvas.ActualWidth, game.Rng.NextDouble()*canvas.ActualHeight),
                    Radius = 10,
                    Life = new LifetimeSource(),
                    Hue = game.Rng.NextDouble()*3
                });
            }

            game.Loop();
        }
    }
}
