using System;
using System.Linq;
using System.Windows;
using SnipSnap.Mathematics;
using TwistedOak.Util;
using TwistedOak.Collections;

namespace SnipSnap {
    public static class GameMechanicUtilities {
        ///<summary>Adds a ball to the game, based on a parent ball (does not handle connecting the child ball to the parent ball).</summary>
        public static Ball SpawnBall(this Game game, Ball parent) {
            // generate an angle that's not too aligned with either the X or Y axies
            var omit = 0.1;
            var quarterTurnet = Math.PI / 2;
            var theta = game.Rng.NextDouble() * quarterTurnet * (1 - 2 * omit)
                      + quarterTurnet * omit
                      + game.Rng.Next(4) * quarterTurnet;

            // create the child ball
            var ball = new Ball {
                Pos = parent.Pos,
                LastPos = parent.Pos,
                Radius = 0.8 * parent.Radius,
                Life = game.Life.CreateDependentSource(),
                Generation = parent.Generation + 1,
                Hue = parent.Hue + game.Rng.NextDouble() * 0.4,
                Vel = parent.Vel + 30 * new Vector(Math.Cos(theta), Math.Sin(theta))
            };

            // if the ball's parent dies, kill it after a small delay
            parent.Life.Lifetime
                .WhenAfterLife(() => game.Delay(TimeSpan.FromMilliseconds(100 + game.Rng.NextDouble() * 100)))
                .WhenDead(ball.Life.EndLifetime);
            
            game.Balls.Add(ball, ball.Life.Lifetime);
            return ball;
        }

        ///<summary>Calls a callback at a somewhat random somewhat regular rate with respect to game time.</summary>
        public static void StochasticRate(this Game game, double expectedPerSecond, Lifetime lifetime, Action callback) {
            game.LoopActions.Add(
                step => {
                    // this is just a rough approximation, not exact at all
                    var rate = expectedPerSecond*step.TimeStep.TotalSeconds;
                    var chance = rate;
                    if (game.Rng.NextDouble() < chance) callback();
                },
                lifetime);
        }

        ///<summary>Handles periodically adding children to existing balls.</summary>
        public static void SetupPeriodicChildSpawning(this Game game, double expectedPerBallPerSecond, int maxChildrenPerBall, int maxGeneration) {
            game.Balls.AsObservable().Subscribe(
                ball => {
                    if (ball.Value.Generation > maxGeneration) return;
                    
                    // keep track of children
                    var curChildCount = 0;
                    var children = new PerishableCollection<Ball>();
                    children
                        .AsObservable()
                        .ObserveNonPerishedCount(completeWhenSourceCompletes: true)
                        .Subscribe(e => curChildCount = e, ball.Lifetime);
                    
                    // spawn children periodically at random
                    game.StochasticRate(
                        expectedPerBallPerSecond,
                        ball.Lifetime,
                        () => {
                            if (curChildCount >= maxChildrenPerBall) return;
                            var child = game.SpawnBall(parent: ball.Value);
                            game.Connectors.Add(new Connector {Child = child, Parent = ball.Value}, child.Life.Lifetime);
                            children.Add(child, child.Life.Lifetime);
                        });
                },
                game.Life);
        }

        ///<summary>Handles making linked balls gently accelerate towards each other.</summary>
        public static void SetupAttractBalls(this Game game, double deadRadius, double accellerationPerSecondChild, double accellerationPerSecondParent) {
            game.LoopActions.Add(
                step => {
                    foreach (var e in game.Connectors.CurrentItems().Select(e => e.Value).Where(e => e.Line.Delta.Length > deadRadius)) {
                        e.Child.Vel -= accellerationPerSecondChild * step.TimeStep.TotalSeconds * e.Line.Delta.Normal();
                        e.Parent.Vel += accellerationPerSecondParent * step.TimeStep.TotalSeconds * e.Line.Delta.Normal();
                    }
                },
                game.Life);
        }

        ///<summary>Handles moving balls at their current velocity and mirroring that velocity when past a boundary.</summary>
        public static void SetupMoveAndBounceBalls(this Game game, Func<Rect> playArea) {
            game.LoopActions.Add(
                step => {
                    foreach (var e in game.Balls.CurrentItems()) {
                        var ball = e.Value;

                        // move
                        ball.LastPos = ball.Pos;
                        ball.Pos += ball.Vel*step.TimeStep.TotalSeconds;

                        // naive bounce back after going off the side
                        var r = playArea();
                        var vx = ball.Vel.X.RangeBounceVelocity(ball.Pos.X, r.Left + ball.Radius, r.Left + ball.Radius + (r.Width - ball.Radius * 2).Max(0));
                        var vy = ball.Vel.Y.RangeBounceVelocity(ball.Pos.Y, r.Top + ball.Radius, r.Top + ball.Radius + (r.Height - ball.Radius * 2).Max(0));
                        ball.Vel = new Vector(vx, vy);
                    }
                },
                game.Life);
        }

        ///<summary>Handles cutting connectors with a moving kill point.</summary>
        public static void SetupMouseCutter(this Game game,
                                            PerishableCollection<UIElement> controls,
                                            Func<Point?> liveMousePosition,
                                            double cutTolerance) {
            var lastUsedMousePos = liveMousePosition();
            game.LoopActions.Add(
                step => {
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