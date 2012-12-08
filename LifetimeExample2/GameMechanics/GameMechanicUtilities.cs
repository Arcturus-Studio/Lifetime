using System;
using System.Windows;
using LifetimeExample.Mathematics;
using TwistedOak.Util;

namespace LifetimeExample2 {
    public static class GameMechanicUtilities {
        public static void SetupMoveAndBounceBalls(this Game game, Func<Rect> playArea) {
            game.LoopActions.Add(
                iter => {
                    foreach (var e in game.Balls.CurrentItems()) {
                        var ball = e.Value;

                        // move
                        ball.LastPos = ball.Pos;
                        ball.Pos += ball.Vel*iter.dt.TotalSeconds;

                        // naive bounce back after going off the side
                        var r = playArea();
                        var vx = ball.Vel.X.RangeBounceVelocity(ball.Pos.X, r.Left, (r.Right - ball.Radius*2).Max(0));
                        var vy = ball.Vel.Y.RangeBounceVelocity(ball.Pos.Y, r.Top, (r.Bottom - ball.Radius*2).Max(0));
                        ball.Vel = new Vector(vx, vy);
                    }
                },
                game.Life);
        }
        public static void SetupMouseCutter(this Game game,
                                            PerishableCollection<UIElement> controls,
                                            Func<Point?> liveMousePosition,
                                            double cutTolerance) {
            var lastUsedMousePos = liveMousePosition();
            game.LoopActions.Add(
                iter => {
                    // get a path between last and current mouse positions, if any
                    var prev = lastUsedMousePos;
                    var cur = liveMousePosition();
                    lastUsedMousePos = cur;
                    if (!prev.HasValue || !cur.HasValue) return;
                    var cutPath = new LineSegment(prev.Value, cur.Value);

                    // cut any connectors that crossed the cut path
                    foreach (var cutConnector in from connector in game.Connectors.CurrentItems()
                                                 let endPath1 = new LineSegment(connector.Parent.LastPos, connector.Parent.Pos)
                                                 let endPath2 = new LineSegment(connector.Child.LastPos, connector.Child.Pos)
                                                 where cutPath.ApproximateMinDistanceFromPointToLineOverTime(endPath1, endPath2) <= cutTolerance
                                                 select connector) {
                        cutConnector.Value.CutPoint = cur.Value;
                        cutConnector.Value.Child.Life.EndLifetime();
                    }
                },
                game.Life);
        }
    }
}