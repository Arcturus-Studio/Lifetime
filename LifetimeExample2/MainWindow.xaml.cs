using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SnipSnap.Mathematics;
using Strilanc.Util;

namespace SnipSnap {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            
            // setup start/stop button
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
            // controls added to this collection should be displayed on the canvas until they perish
            var controls = new PerishableCollection<UIElement>();
            controls.AsObservable().Subscribe(
                e => {
                    canvas.Children.Add(e.Value);
                    e.Lifetime.WhenDead(() => canvas.Children.Remove(e.Value));
                },
                game.Life);

            // balls should move and bounce off borders
            game.SetupMoveAndBounceBalls(
                playArea: () => new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));

            // connected balls should be be gently tugged towards each other
            game.SetupAttractBalls(
                deadRadius: 50,
                accellerationPerSecondChild: 10,
                accellerationPerSecondParent: 5);

            // balls should periodically spawn dependent children
            game.SetupPeriodicChildSpawning(
                expectedPerBallPerSecond: 0.2, 
                maxChildrenPerBall: 2, 
                maxGeneration: 5);

            // balls should be drawn using ellipse controls and have death animations
            game.SetupDrawBallsAsControls(
                controls, 
                deathFadeOutDuration: 800.Milliseconds(), 
                deathFinalRadiusFactor: 3);

            // ball connectors should be drawn using line controls and have cut and death animations
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

            // connectors that touch the cursor should die
            SetupMouseCutter(game, controls);

            // text displays of game state should track that state
            SetupDisplayLabels(game);

            // there should be a few root balls to start with
            foreach (var repeat in 5.Range()) {
                game.SpawnBall(parent: new Ball {
                    Pos = new Point(game.Rng.NextDouble()*canvas.ActualWidth, game.Rng.NextDouble()*canvas.ActualHeight),
                    Radius = 10,
                    Life = new LifetimeSource(),
                    Hue = game.Rng.NextDouble()*3
                });
            }

            // run the game loop until the game is over
            game.Loop().ContinueWith(e => {
                // exceptions?
            });
        }

        private void SetupMouseCutter(Game game, PerishableCollection<UIElement> controls) {
            // create rectangle to center under mouse
            var rotater = new RotateTransform();
            var translater = new TranslateTransform();
            var mouseTarget = new Rectangle {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.Black),
                IsHitTestVisible = false,
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
            
            game.LoopActions.Add(step => {
                elapsed += step.TimeStep;
                TimeLabel.Text = String.Format("Time: {0:0.0}s", elapsed.TotalSeconds);
            }, game.Life);
        }
    }
}
