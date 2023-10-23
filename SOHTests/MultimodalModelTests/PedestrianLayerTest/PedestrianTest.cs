using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHDomain.Common;
using SOHDomain.Steering.Handles;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Multimodal;
using SOHMultimodalModel.Routing;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.PedestrianLayerTest;

public class PedestrianTest
{
    [Fact]
    public void ChangeWalkToRun()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var pedestrian = new TestPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        pedestrian.Init(multimodalLayer);
        Assert.Equal(GenderType.Male, pedestrian.Gender);

        var route = environment.GraphEnvironment.FindRoute(environment.Node1, environment.Node2);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        pedestrian.SetRunning();
        Assert.InRange(pedestrian.PreferredSpeed,
            HumanVelocityConstants.MeanValueRunMale - HumanVelocityConstants.DeviationRunMale,
            HumanVelocityConstants.MeanValueRunMale + HumanVelocityConstants.DeviationRunMale);
        pedestrian.Tick();
        Assert.InRange(pedestrian.Velocity,
            HumanVelocityConstants.MeanValueRunMale - HumanVelocityConstants.DeviationRunMale,
            HumanVelocityConstants.MeanValueRunMale + HumanVelocityConstants.DeviationRunMale);

        pedestrian.SetWalking();
        Assert.InRange(pedestrian.PreferredSpeed,
            HumanVelocityConstants.MeanValueWalkMale - HumanVelocityConstants.DeviationWalkMale,
            HumanVelocityConstants.MeanValueWalkMale + HumanVelocityConstants.DeviationWalkMale);
        pedestrian.Tick();
        Assert.InRange(pedestrian.Velocity,
            HumanVelocityConstants.MeanValueWalkMale - HumanVelocityConstants.DeviationWalkMale,
            HumanVelocityConstants.MeanValueWalkMale + HumanVelocityConstants.DeviationWalkMale);
    }

    [Fact]
    public void InvalidateActiveSteeringOnLeavingSidewalk()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var goalNode = environment.Node2;

        var pedestrian = new TestPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        pedestrian.Init(multimodalLayer);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.False(pedestrian.OnSidewalk);

        var route = environment.GraphEnvironment.FindRoute(startNode, goalNode);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
        pedestrian.Tick();
        Assert.NotNull(pedestrian.ActiveSteeringHandle.Route);

        pedestrian.EnterModalType(pedestrian.MultimodalRoute.CurrentModalChoice,
            pedestrian.MultimodalRoute.CurrentRoute);
        Assert.True(pedestrian.OnSidewalk);

        pedestrian.LeaveModalType(pedestrian.MultimodalRoute.CurrentModalChoice);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.False(pedestrian.OnSidewalk);
    }

    [Fact]
    public void MoveFromInitialNodeSuccessfully()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var goalNode = environment.Node2;

        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);

        var route = environment.GraphEnvironment.FindRoute(startNode, goalNode, (_, edge, _) => edge.Length);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        Assert.Equal(0, pedestrian.Velocity);
        Assert.Equal(startNode.Position, pedestrian.Position);

        pedestrian.Move();

        Assert.True(pedestrian.Velocity > 0);
        Assert.NotEqual(startNode.Position, pedestrian.Position);
    }

    [Fact]
    public void MoveFromNode1ToNode2()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var goalNode = environment.Node2;

        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);

        var route = environment.GraphEnvironment.FindRoute(startNode, goalNode);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        pedestrian.Move();
        Assert.NotEqual(startNode.Position, pedestrian.Position);

        for (var tick = 0; tick < 200 && !pedestrian.GoalReached; tick++) pedestrian.Move();

        Assert.Equal(goalNode.Position.X, pedestrian.Position.X, 3);
        Assert.Equal(goalNode.Position.Y, pedestrian.Position.Y, 3);
    }

    [Fact]
    public void PositionCorrectAfterEnteringAndLeavingSidewalk()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startPosition = FourNodeGraphEnv.Node1Pos;

        var pedestrian = new TestPedestrian
        {
            StartPosition = startPosition
        };
        pedestrian.Init(multimodalLayer);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.Equal(startPosition, pedestrian.Position);
        Assert.Equal(Whereabouts.Offside, pedestrian.Whereabouts);

        var route = environment.GraphEnvironment.FindShortestRoute(environment.Node1, environment.Node2);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.Equal(startPosition, pedestrian.Position);
        Assert.Equal(Whereabouts.Offside, pedestrian.Whereabouts);

        pedestrian.EnterModalType(pedestrian.MultimodalRoute.CurrentModalChoice,
            pedestrian.MultimodalRoute.CurrentRoute);
        Assert.NotNull(pedestrian.ActiveSteeringHandle);
        Assert.Equal(startPosition, pedestrian.Position);
        Assert.Equal(Whereabouts.Sidewalk, pedestrian.Whereabouts);

        pedestrian.LeaveModalType(pedestrian.MultimodalRoute.CurrentModalChoice);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.Equal(startPosition, pedestrian.Position);
        Assert.Equal(Whereabouts.Offside, pedestrian.Whereabouts);
    }

    [Fact]
    public void PositionParameterIsUpdated()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var node1 = environment.Node1;
        var node2 = environment.Node2;
        var pedestrian = new TestPedestrian
        {
            StartPosition = node1.Position
        };
        pedestrian.Init(multimodalLayer);
        Assert.Equal(node1.Position, pedestrian.Position);

        var route = environment.GraphEnvironment.FindRoute(node1, node2, (_, edge, _) => edge.Length);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.Equal(node1.Position, pedestrian.Position);

        pedestrian.Move();
        Assert.NotNull(pedestrian.ActiveSteeringHandle);
        Assert.True(pedestrian.ActiveSteeringHandle is WalkingSteeringHandle);
        Assert.NotEqual(node1.Position, pedestrian.Position);
    }

    [Fact]
    public void PositionPreservedAfterMoveAndLeavingSidewalk()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var goalNode = environment.Node2;

        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);
        Assert.Equal(startNode.Position, pedestrian.Position);

        var route = environment.GraphEnvironment.FindRoute(startNode, goalNode);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
        Assert.Equal(startNode.Position, pedestrian.Position);

        pedestrian.Move();
        var currentPosition = pedestrian.Position;

        // pedestrian.LeaveSidewalk();
        Assert.Equal(currentPosition, pedestrian.Position);
    }

    [Fact]
    public void ResetRoadUserParameters()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);
        Assert.Null(pedestrian.CurrentEdge);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);

        var route = environment.GraphEnvironment.FindRoute(startNode, environment.Node2);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
        pedestrian.Move();
        Assert.NotNull(pedestrian.CurrentEdge);
        Assert.NotEqual(0, pedestrian.PositionOnCurrentEdge);

        pedestrian.LeaveModalType(pedestrian.MultimodalRoute.CurrentModalChoice);
        Assert.Null(pedestrian.CurrentEdge);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);
    }

    [Fact]
    public void RunningAgentOvertakesWalkingAgent()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        Assert.True(HumanVelocityConstants.MeanValueRunMale - HumanVelocityConstants.DeviationRunMale >
                    HumanVelocityConstants.MeanValueWalkMale + HumanVelocityConstants.DeviationWalkMale);

        var walkingAgent = new TestPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        walkingAgent.Init(multimodalLayer);
        var runningAgent = new TestPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        runningAgent.Init(multimodalLayer);

        var graphEnvironment = environment.GraphEnvironment;
        walkingAgent.MultimodalRoute =
            new MultimodalRoute(graphEnvironment.FindRoute(environment.Node1, environment.Node2),
                ModalChoice.Walking);
        runningAgent.MultimodalRoute =
            new MultimodalRoute(graphEnvironment.FindRoute(environment.Node1, environment.Node2),
                ModalChoice.Walking);


        walkingAgent.SetWalking();
        runningAgent.SetRunning();

        Assert.Equal(FourNodeGraphEnv.Node1Pos, walkingAgent.Position);
        Assert.Equal(FourNodeGraphEnv.Node1Pos, runningAgent.Position);

        walkingAgent.Tick(); // move first
        Assert.NotEqual(FourNodeGraphEnv.Node1Pos, walkingAgent.Position);

        for (var tick = 0; tick < 5; tick++)
        {
            walkingAgent.Move();
            runningAgent.Move();
        }

        Assert.True(runningAgent.Velocity > walkingAgent.Velocity);
        Assert.True(runningAgent.PositionOnCurrentEdge > walkingAgent.PositionOnCurrentEdge);
    }

    [Fact]
    public void SwitchRouteOnTheWay()
    {
        var fourNodeEnv = new FourNodeGraphEnv();
        var env = fourNodeEnv.GraphEnvironment;
        var multimodalLayer = new TestMultimodalLayer(fourNodeEnv.GraphEnvironment);
        var pedestrian = new TestPedestrian
        {
            StartPosition = fourNodeEnv.Node1.Position
        };
        pedestrian.Init(multimodalLayer);

        var route = env.FindRoute(fourNodeEnv.Node1, fourNodeEnv.Node4, (_, edge, _) => edge.Length);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        var onWayToNode4 = true;
        for (var tick = 0; tick < 10000 && !pedestrian.GoalReached; tick++)
        {
            pedestrian.Move();

            if (onWayToNode4 && route.First().Edge.To.Equals(fourNodeEnv.Node4))
            {
                var routeToNode3 = env.FindRoute(fourNodeEnv.Node1, fourNodeEnv.Node3,
                    (_, edge, _) => edge.Length);
                Assert.InRange(route.RouteLength - route.RemainingRouteDistanceToGoal, routeToNode3.RouteLength,
                    route.RouteLength);

                // go back
                onWayToNode4 = false;
                var currentNode = route.First().Edge.From;
                route = env.FindRoute(currentNode, fourNodeEnv.Node1, (_, edge, _) => edge.Length);
                pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
            }
        }

        Assert.True(pedestrian.GoalReached);
        Assert.InRange(fourNodeEnv.Node1.Position.DistanceInKmTo(pedestrian.Position), 0, 0.005);
        Assert.Equal(fourNodeEnv.Node1.Position.Latitude, pedestrian.Position.Latitude, 3);
        Assert.Equal(fourNodeEnv.Node1.Position.Longitude, pedestrian.Position.Longitude, 3);
    }

    [Fact]
    public void SwitchRouteWithinMoveProceedOnSameEdge()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var environment = fourNodeGraphEnv.GraphEnvironment;

        var multimodalLayer = new TestMultimodalLayer(environment);
        var startNode = fourNodeGraphEnv.Node1;

        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.Null(pedestrian.CurrentEdge);
        Assert.False(pedestrian.OnSidewalk);

        var firstRoute = environment.FindRoute(startNode, fourNodeGraphEnv.Node2, (_, edge, _) => edge.Length);
        pedestrian.MultimodalRoute = new MultimodalRoute(firstRoute, ModalChoice.Walking);
        pedestrian.Move();
        Assert.NotNull(pedestrian.CurrentEdge);
        Assert.NotEqual(0, pedestrian.PositionOnCurrentEdge);

        var secondRoute = environment.FindRoute(startNode, fourNodeGraphEnv.Node3, (_, edge, _) => edge.Length);
        Assert.Equal(secondRoute.First().Edge, pedestrian.CurrentEdge);

        pedestrian.MultimodalRoute = new MultimodalRoute(secondRoute, ModalChoice.Walking);
        var oldPositionOnCurrentEdge = pedestrian.PositionOnCurrentEdge;
        pedestrian.Move();

        Assert.Equal(firstRoute.First().Edge, pedestrian.CurrentEdge);
        Assert.Equal(secondRoute.First().Edge, pedestrian.CurrentEdge);
        Assert.InRange(pedestrian.PositionOnCurrentEdge, oldPositionOnCurrentEdge, firstRoute.First().Edge.Length);
    }

    [Fact]
    public void SwitchRouteWithinMoveRequiresJump()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);
        var startNode = environment.Node1;
        var pedestrian = new TestPedestrian
        {
            StartPosition = startNode.Position
        };
        pedestrian.Init(multimodalLayer);
        Assert.Null(pedestrian.ActiveSteeringHandle);
        Assert.False(pedestrian.OnSidewalk);

        var firstRoute = environment.GraphEnvironment.FindRoute(startNode, environment.Node2);
        pedestrian.MultimodalRoute = new MultimodalRoute(firstRoute, ModalChoice.Walking);
        pedestrian.Move();
        Assert.NotNull(pedestrian.ActiveSteeringHandle);
        Assert.NotNull(pedestrian.CurrentEdge);
        Assert.NotEqual(0, pedestrian.PositionOnCurrentEdge);

        var secondRoute = environment.GraphEnvironment.FindRoute(environment.Node2, environment.Node3);
        Assert.NotEqual(secondRoute.First().Edge, pedestrian.CurrentEdge);

        pedestrian.MultimodalRoute = new MultimodalRoute(secondRoute, ModalChoice.Walking);
        pedestrian.Move();

        //jump to new route
        Assert.Equal(secondRoute.First().Edge, pedestrian.CurrentEdge);
        Assert.NotEqual(firstRoute.First().Edge, pedestrian.CurrentEdge);
        Assert.NotEqual(0, pedestrian.PositionOnCurrentEdge);
    }

    [Fact]
    public void WalkFullFourNodeEnvironment()
    {
        var environment = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(environment.GraphEnvironment);
        var start = FourNodeGraphEnv.Node1Pos;
        var goal = environment.Node4.Position;
        var pedestrian = new TestPedestrian
        {
            StartPosition = start
        };
        pedestrian.Init(layer);
        // pedestrian.TryEnterVehicleAsDriver(this);
        // pedestrian.EnterSidewalk(pedestrian.Position);
        Assert.Equal(start, pedestrian.Position);

        pedestrian.MultimodalRoute = new WalkingMultimodalRoute(layer.SpatialGraphMediatorLayer, start, goal);

        for (var tick = 0; tick < 5000 && !pedestrian.GoalReached; tick++, layer.Context.UpdateStep())
            pedestrian.Tick();
        Assert.True(pedestrian.GoalReached);

        Assert.InRange(goal.DistanceInMTo(pedestrian.Position), 0, 3);
        Assert.Equal(goal, pedestrian.Position);
    }

    [Fact]
    public void WalkToReachNodeOnAltonaGraph()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        var multimodalLayer = new TestMultimodalLayer(environment);

        var start = environment.NearestNode(Position.CreateGeoPosition(9.845780, 53.570825));
        var goal = environment.NearestNode(Position.CreateGeoPosition(9.847038, 53.571780));

        var pedestrian = new TestPedestrian
        {
            StartPosition = start.Position
        };
        pedestrian.Init(multimodalLayer);

        Assert.Equal(start.Position, pedestrian.Position);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);
        Assert.Null(pedestrian.CurrentEdge);

        var route = environment.FindRoute(start, goal);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        for (var tick = 0; tick < 100; tick++)
        {
            pedestrian.Move();
            if (pedestrian.GoalReached) break;
        }

        Assert.True(pedestrian.GoalReached);
        Assert.InRange(goal.Position.DistanceInKmTo(pedestrian.Position), 0, 001);
        Assert.Equal(goal.Position.Latitude, pedestrian.Position.Latitude, 10);
        Assert.Equal(goal.Position.Longitude, pedestrian.Position.Longitude, 10);
    }

    [Fact]
    public void WalkToReachNodeOnSimpleEnvironment()
    {
        var environment = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(environment.GraphEnvironment);

        var pedestrian = new TestPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        pedestrian.Init(multimodalLayer);

        Assert.Equal(FourNodeGraphEnv.Node1Pos, pedestrian.Position);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);
        Assert.Null(pedestrian.CurrentEdge);

        pedestrian.Move();

        //no change, because of missing route
        Assert.Equal(FourNodeGraphEnv.Node1Pos, pedestrian.Position);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);
        Assert.Null(pedestrian.CurrentEdge);

        var route = environment.GraphEnvironment.FindRoute(environment.Node1, environment.Node2);
        pedestrian.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        //no change without movement
        Assert.Equal(FourNodeGraphEnv.Node1Pos, pedestrian.Position);
        Assert.Equal(0, pedestrian.PositionOnCurrentEdge);
        Assert.Null(pedestrian.CurrentEdge);

        for (var tick = 0; tick < 10000; tick++)
        {
            pedestrian.Move();
            if (pedestrian.GoalReached) break;
        }

        Assert.True(pedestrian.GoalReached);
        Assert.InRange(environment.Node2.Position.DistanceInKmTo(pedestrian.Position), 0, 001);
        Assert.Equal(environment.Node2.Position.Latitude, pedestrian.Position.Latitude, 2);
        Assert.Equal(environment.Node2.Position.Longitude, pedestrian.Position.Longitude, 2);
    }

    private class TestPedestrian : TestMultiCapableAgent
    {
        public ISteeringHandle ActiveSteeringHandle => ActiveSteering;

        public new bool OnSidewalk => base.OnSidewalk;
        public double PositionOnCurrentEdge => WalkingShoes.PositionOnCurrentEdge;
        public ISpatialEdge CurrentEdge => WalkingShoes.CurrentEdge;

        public new void EnterModalType(ModalChoice modalChoice, Route route)
        {
            base.EnterModalType(modalChoice, route);
        }

        public new void LeaveModalType(ModalChoice modalChoice)
        {
            base.LeaveModalType(modalChoice);
        }

        public override void Tick()
        {
            Move();
        }
    }
}