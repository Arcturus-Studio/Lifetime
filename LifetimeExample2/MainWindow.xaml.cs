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
                RunGame(new Game());
            };
        }
        private void RunGame(Game game) {
            var controls = new PerishableCollection<UIElement>();
            controls.AsObservable().Subscribe(
                e => {
                    canvas.Children.Add(e.Value);
                    e.Lifetime.WhenDead(() => canvas.Children.Remove(e.Value));
                },
                game.Life);

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

            var liveMousePos = default(Point?);
            this.MouseMove += (sender, arg) => liveMousePos = arg.GetPosition(canvas);
            this.MouseLeave += (sender, arg) => liveMousePos = null;
            game.SetupMouseCutter(
                controls,
                () => liveMousePos,
                cutTolerance: 1);


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
    }
}
