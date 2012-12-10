using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SnipSnap.Mathematics;
using TwistedOak.Util;

namespace SnipSnap {
    public partial class MainWindow {
        public TimeSpan Best = TimeSpan.Zero;

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
                propagateBangColor: Colors.Green,
                propagateBangDuration: 400.Milliseconds(),
                propagateBangRotationsPerSecond: 3,
                propagateBangMaxRadius: 10,
                cutBangColor: Colors.Red,
                cutBangDuration: 400.Milliseconds(),
                cutBangRotationsPerSecond: 2,
                cutBangMaxRadius: 15);

            // connectors that touch the cursor should die
            SetupMouseCutter(game, controls);

            // text displays of game state should track that state
            SetupEnergyAndTime(
                game,
                energyLossForCutting: 2.5,
                energyGainPerConnectorBroken: 1);

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
                Visibility = Visibility.Collapsed,
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
        private void SetupEnergyAndTime(Game game, double energyLossForCutting, double energyGainPerConnectorBroken) {
            var elapsed = TimeSpan.Zero;

            // show energy status
            var done = false;
            var gains = 0.0;
            var loses = 0.0;
            var energy = 50.0;
            var maxEnergy = 50.0;
            game.LoopActions.Add(step => {
                // energy decays faster and faster over time
                var t = step.TimeStep.TotalSeconds;
                energy -= t * Math.Log(elapsed.TotalSeconds / 15 + 1).Max(1);

                // quickly decrease the size of the red/blue bars that show changes
                gains -= t * 5;
                loses -= t * 5;
                gains *= Math.Pow(0.01, t);
                loses *= Math.Pow(0.01, t);
                
                // keep everything reasonable
                gains = gains.Clamp(0, 10);
                loses = loses.Clamp(0, 10);
                energy = energy.Clamp(0, maxEnergy);

                // "draw" energy
                var w = canvas.ActualWidth;
                BarEnergy.Fill = new SolidColorBrush(Colors.Yellow.LerpTo(Colors.Green, (energy * 2 / maxEnergy).Clamp(0, 1)));
                BarLoses.Width = ((energy + loses) / maxEnergy * w).Clamp(0, w);
                BarGains.Width = (energy / maxEnergy * w).Clamp(0, w);
                BarEnergy.Width = ((energy - gains) / maxEnergy * w).Clamp(0, w);

                // hit 0 energy? game over
                done |= energy == 0;
            }, game.Life);

            // track energy changes due to connectors dying
            game.Connectors.AsObservable().Subscribe(e => e.Lifetime.WhenDead(() => {
                // breaking connectors gain you some energy
                if (!done) energy += energyGainPerConnectorBroken;
                gains += energyGainPerConnectorBroken;

                // but making cuts costs energy
                if (e.Value.CutPoint != null) {
                    energy -= energyLossForCutting;
                    loses += energyLossForCutting;
                }
            }), game.Life);
            
            // track times
            game.LoopActions.Add(step => {
                // advance time
                if (!done) elapsed += step.TimeStep;
                Best = Best.Max(elapsed);
                
                // show time (red when dead)
                TimeLabel.Background = new SolidColorBrush(done ? Colors.Pink : Colors.White);
                TimeLabel.Text = String.Format("Time: {0:0.0}s", elapsed.TotalSeconds);

                // show best time (green when making best time)
                TimeBest.Background = new SolidColorBrush(Best == elapsed ? Colors.LightGreen : Colors.White);
                TimeBest.Text = String.Format("Best: {0:0.0}s", Best.TotalSeconds);
            }, game.Life);
        }
    }
}
