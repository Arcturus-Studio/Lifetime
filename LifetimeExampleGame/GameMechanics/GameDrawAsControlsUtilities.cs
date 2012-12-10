using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SnipSnap.Mathematics;
using TwistedOak.Collections;
using TwistedOak.Util;
using LineSegment = SnipSnap.Mathematics.LineSegment;

namespace SnipSnap {
    ///<summary>Utility methods for naively showing game state via WPF.</summary>
    public static class GameDrawAsControlsUtilities {
        ///<summary>Positions a line control to match a line segment.</summary>
        public static void Reposition(this Line lineControl, LineSegment position) {
            lineControl.X1 = position.Start.X;
            lineControl.Y1 = position.Start.Y;
            lineControl.X2 = position.End.X;
            lineControl.Y2 = position.End.Y;
        }

        ///<summary>Positions an ellipse control to match a center and radius.</summary>
        public static void Reposition(this Ellipse ellipseControl, Point center, double radius) {
            ellipseControl.Width = ellipseControl.Height = radius * 2;
            ellipseControl.RenderTransform = new TranslateTransform(center.X - radius, center.Y - radius);
        }

        ///<summary>Causes a rotating rectangle that grows and fades in then shrinks and fades out to be shown.</summary>
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
                (step, portion, ellapsed) => {
                    var nearHalfPortion = (portion - 0.5).Abs()*2;
                    var radius = 0.LerpTo(finalRadius, 1 - nearHalfPortion);
                    var pos = position(portion);
                    rect.Width = rect.Height = radius*2;
                    rect.RenderTransform = new TransformGroup {
                        Children = new TransformCollection {
                            new RotateTransform(rotationsPerSecond*ellapsed.TotalSeconds*360),
                            new TranslateTransform(pos.X - radius, pos.Y - radius)
                        }
                    };
                    rect.Fill = new SolidColorBrush(color.LerpToTransparent(nearHalfPortion));
                });
            controls.Add(rect, life);
            return life;
        }

        ///<summary>Handles making line controls for each connector in the game, as while as showing their death animations.</summary>
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
                        step => lineControl.Reposition(con.Line),
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
                        (step, portion, dt) => {
                            lineControl.StrokeThickness = thickness * 1.LerpTo(deathFinalThicknessFactor, portion);
                            lineControl.Stroke = new SolidColorBrush(Colors.Black.LerpToTransparent(portion));
                            lineControl.Reposition(e.Value.Line);
                        }));

                    controls.Add(lineControl, controlLife);
                },
                game.Life);
        }

        ///<summary>Handles making ellipse controls for each ball in the game, as while as showing their death animations.</summary>
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

                    // 'root' balls have a black border
                    if (ball.Generation == 1) {
                        ellipseControl.StrokeThickness = 3;
                        ellipseControl.Stroke = new SolidColorBrush(Colors.Black);
                    }

                    // reposition ellipse control during each game loop, until ball is dead
                    game.LoopActions.Add(
                        step => ellipseControl.Reposition(ball.Pos, ball.Radius),
                        e.Lifetime);

                    // once ball is dead, expand and fade out the ellipse
                    var controlLifetime = e.Lifetime.WhenAfterLife(() =>
                        game.AnimateWith(
                            deathFadeOutDuration,
                            (step, portion, dt) => {
                                // fade out
                                ellipseControl.Fill = new SolidColorBrush(color.LerpToTransparent(portion));
                                // expand
                                var radius = ball.Radius * 1.LerpTo(deathFinalRadiusFactor, portion);
                                ellipseControl.Reposition(ball.Pos, radius);
                            }));

                    controls.Add(ellipseControl, controlLifetime);
                },
                game.Life);
        }
    }
}