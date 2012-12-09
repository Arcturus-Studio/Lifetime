using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using LifetimeExample.Mathematics;
using TwistedOak.Util;

namespace LifetimeExample2 {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            
            Game curGame = null;
            ButtonStartStop.Click += (sender, arg) => {
                if (curGame == null) {
                    curGame = new Game();
                    SetupAndRunGame(curGame);
                    ButtonStartStop.Content = "End";
                } else {
                    curGame.LifeSource.EndLifetime();
                    curGame = null;
                    ButtonStartStop.Content = "Start";
                }
            };
        }
        private void SetupAndRunGame(Game game) {
            var controls = new PerishableCollection<UIElement>();
            controls.AsObservable().Subscribe(
                e => {
                    canvas.Children.Add(e.Value);
                    e.Lifetime.WhenDead(() => canvas.Children.Remove(e.Value));
                },
                game.Life);

            SetupDisplayLabels(game);

            game.SetupMoveAndBounceBalls(() => new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));

            game.SetupPeriodicChildSpawning(
                expectedPerBallPerSecond: 0.2, 
                maxChildrenPerBall: 2, 
                maxGeneration: 5);

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

            SetupMouseCutter(game, controls);

            // spawn some root balls
            foreach (var repeat in 5.Range()) {
                game.SpawnBall(parent: new Ball {
                    Pos = new Point(game.Rng.NextDouble()*canvas.ActualWidth, game.Rng.NextDouble()*canvas.ActualHeight),
                    Radius = 10,
                    Life = new LifetimeSource(),
                    Hue = game.Rng.NextDouble()*3
                });
            }

            game.Loop();
        }
        private void SetupMouseCutter(Game game, PerishableCollection<UIElement> controls) {
            // create rectangle to center under mouse
            var rotater = new RotateTransform();
            var translater = new TranslateTransform();
            var mouseTarget = new Rectangle {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.Black),
                RenderTransform = new TransformGroup {
                    Children = new TransformCollection {
                        rotater,
                        translater
                    }
                },
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            controls.Add(mouseTarget, game.Life);
            
            // make the rectangle rotate
            rotater.BeginAnimation(
                RotateTransform.AngleProperty, 
                new DoubleAnimation(0, 360, 1.Seconds()) { RepeatBehavior = RepeatBehavior.Forever });
            
            // watch mouse position over canvas, keeping the rotating rectangle centered on it
            var liveMousePos = default(Point?);
            MouseEventHandler h = (sender, arg) => {
                mouseTarget.Visibility = Visibility.Visible;
                liveMousePos = arg.GetPosition(canvas);
                translater.X = liveMousePos.Value.X - mouseTarget.Width / 2;
                translater.Y = liveMousePos.Value.Y - mouseTarget.Height / 2;
            };
            MouseEventHandler h2 = (sender, arg) => {
                mouseTarget.Visibility = Visibility.Collapsed;
                liveMousePos = null;
            };
            canvas.MouseMove += h;
            canvas.MouseLeave += h2;
            game.Life.WhenDead(() => canvas.MouseMove -= h);
            game.Life.WhenDead(() => canvas.MouseLeave -= h2);
            
            // pass along mouse positions to the game
            game.SetupMouseCutter(
                controls,
                () => liveMousePos,
                cutTolerance: 5);
        }
        private void SetupDisplayLabels(Game game) {
            var snips = 0;
            var snaps = 0;
            var elapsed = TimeSpan.Zero;
            
            TimeLabel.Text = String.Format("Time: {0:0.0}s", elapsed.TotalSeconds);
            SnapsLabel.Text = String.Format("Snaps: {0}", snaps);
            SnipsLabel.Text = String.Format("Snips: {0}", snips);
            
            game.Connectors.AsObservable().Subscribe(e => e.Lifetime.WhenDead(() => {
                if (e.Value.CutPoint != null) 
                    snips += 1;
                else
                    snaps += 1;
                SnapsLabel.Text = String.Format("Snaps: {0}", snaps);
                SnipsLabel.Text = String.Format("Snips: {0}", snips);
            }), game.Life);
            
            game.LoopActions.Add(iter => {
                elapsed += iter.dt;
                TimeLabel.Text = String.Format("Time: {0:0.0}s", elapsed.TotalSeconds);
            }, game.Life);
        }
    }
}
