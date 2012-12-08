using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using LifetimeExample.Mathematics;
using TwistedOak.Util;
using LineSegment = LifetimeExample.Mathematics.LineSegment;

namespace LifetimeExample2 {
    public static class GameDrawAsControlsUtilities {
        public static void Reposition(this Line lineControl, LineSegment position) {
            lineControl.X1 = position.Start.X;
            lineControl.Y1 = position.Start.Y;
            lineControl.X2 = position.End.X;
            lineControl.Y2 = position.End.Y;
        }
        
        public static void Reposition(this Ellipse ellipseControl, Point center, double radius) {
            ellipseControl.Width = ellipseControl.Height = radius * 2;
            ellipseControl.RenderTransform = new TranslateTransform(center.X - radius, center.Y - radius);
        }

        public static Lifetime AnimateSpinningRectangleExplosion(this Game game,
                                                                 PerishableCollection<UIElement> controls,
                                                                 Func<double, Point> position,
                                                                 Color color,
                                                                 TimeSpan duration,
                                                                 double rotationsPerSecond,
                                                                 double finalRadius) {
            var rect = new Rectangle {
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var life = game.AnimateWith(
                duration,
                (ix, prop, ellapsed) => {
                    var m = (prop - 0.5).Abs()*2;
                    var r = 0.LerpTo(finalRadius, 1 - m);
                    var pt = position(prop);
                    rect.Width = rect.Height = r*2;
                    rect.RenderTransform = new TransformGroup {
                        Children = new TransformCollection {
                            new RotateTransform(rotationsPerSecond*ellapsed.TotalSeconds*360),
                            new TranslateTransform(pt.X - r, pt.Y - r)
                        }
                    };
                    rect.Fill = new SolidColorBrush(color.LerpToTransparent(m));
                });
            controls.Add(rect, life);
            return life;
        }

        public static void SetupDrawConnectorsAsControls(this Game game,
                                                         PerishableCollection<UIElement> controls,
                                                         TimeSpan deathFadeDuration,
                                                         double deathFinalThicknessFactor,
                                                         Color cutBangColor,
                                                         TimeSpan cutBangDuration,
                                                         double cutBangRotationsPerSecond,
                                                         double cutBangMaxRadius,
                                                         Color propagateBangColor,
                                                         TimeSpan propagateBangDuration,
                                                         double propagateBangRotationsPerSecond,
                                                         double propagateBangMaxRadius) {
            game.Connectors.AsObservable().Subscribe(
                e => {
                    // create a line control to visually represent the line
                    var con = e.Value;
                    var thickness = con.Child.Radius * 0.1;
                    var lineControl = new Line {
                        Stroke = new SolidColorBrush(Colors.Black),
                        StrokeThickness = thickness,
                    };

                    // reposition line control during each game loop, until the connector is dead
                    game.LoopActions.Add(
                        iter => lineControl.Reposition(con.Line),
                        e.Lifetime);

                    // show a bang if the connector is cut
                    e.Lifetime.WhenDead(() => {
                        if (con.CutPoint == null) return;
                        game.AnimateSpinningRectangleExplosion(
                            controls,
                            p => con.CutPoint.Value.ClosestPointOn(con.Line),
                            cutBangColor,
                            cutBangDuration,
                            cutBangRotationsPerSecond,
                            cutBangMaxRadius);
                    });

                    // show a bang travelling along the connector when it dies
                    e.Lifetime.WhenDead(() =>
                        game.AnimateSpinningRectangleExplosion(controls,
                                                               p => (con.CutPoint == null ? con.Line.Start : con.CutPoint.Value.ClosestPointOn(con.Line)).To(e.Value.Line.End).LerpAcross(p),
                                                               propagateBangColor,
                                                               propagateBangDuration,
                                                               propagateBangRotationsPerSecond,
                                                               propagateBangMaxRadius));

                    // expand and fade out the line control after the connector dies
                    var controlLife = e.Lifetime.WhenAfterLife(() => game.AnimateWith(
                        deathFadeDuration,
                        (iter, prop, dt) => {
                            lineControl.StrokeThickness = thickness * 1.LerpTo(deathFinalThicknessFactor, prop);
                            lineControl.Stroke = new SolidColorBrush(Colors.Black.LerpToTransparent(prop));
                            lineControl.Reposition(e.Value.Line);
                        }));

                    controls.Add(lineControl, controlLife);
                },
                game.Life);
        }
        public static void SetupDrawBallsAsControls(this Game game, PerishableCollection<UIElement> controls, TimeSpan deathFadeOutDuration, double deathFinalRadiusFactor) {
            game.Balls.AsObservable().Subscribe(
                e => {
                    // create an ellipse control to visually represent the ball
                    var ball = e.Value;
                    var color = ball.Hue.HueToApproximateColor(period: 3);
                    var ellipseControl = new Ellipse {
                        Width = ball.Radius * 2,
                        Height = ball.Radius * 2,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Fill = new SolidColorBrush(color)
                    };

                    if (ball.Generation == 1) {
                        ellipseControl.StrokeThickness = 3;
                        ellipseControl.Stroke = new SolidColorBrush(Colors.Black);
                    }

                    // reposition ellipse control during each game loop, until ball is dead
                    game.LoopActions.Add(
                        iter => ellipseControl.Reposition(ball.Pos, ball.Radius),
                        e.Lifetime);

                    // once ball is dead, expand and fade out the ellipse
                    var controlLifetime = e.Lifetime.WhenAfterLife(() =>
                        game.AnimateWith(
                            deathFadeOutDuration,
                            (iter, prop, dt) => {
                                // fade out
                                ellipseControl.Fill = new SolidColorBrush(color.LerpToTransparent(prop));
                                // expand
                                var radius = ball.Radius * 1.LerpTo(deathFinalRadiusFactor, prop);
                                ellipseControl.Reposition(ball.Pos, radius);
                            }));

                    controls.Add(ellipseControl, controlLifetime);
                },
                game.Life);
        }
    }
}