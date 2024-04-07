using Mars.Components.Environments;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Multimodal;
using SOHModel.Multimodal.Routing;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.PedestrianLayerTest;

public class PedestrianUsesGatewaysTests
{
    private readonly GatewayLayer _gatewayLayer;
    private readonly TestMultimodalLayer _layer;

    public PedestrianUsesGatewaysTests()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.WalkGraphAltonaAltstadt);
        _layer = new TestMultimodalLayer(environment);

        _gatewayLayer = new GatewayLayer(environment);
        _gatewayLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig = { File = ResourcesConstants.RailroadStations }
        });
        _layer.GatewayLayer = _gatewayLayer;
    }

    [Fact]
    public void MoveWithinEnvironmentValidatingGoalWithGatewayLayer()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.936316, 53.5478216);
        Assert.InRange(start.DistanceInMTo(goal), 200, 1000);

        var gateway = _gatewayLayer.Validate(start, goal).Item2;
        Assert.Equal(goal, gateway);

        var agent = new TestWalkingGatewayPedestrian
        {
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);

        for (var tick = 0; tick < 10000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep()) agent.Tick();
        Assert.True(agent.GoalReached);

        var goalNode = _layer.SidewalkEnvironment.NearestNode(gateway);
        Assert.InRange(goalNode.Position.DistanceInMTo(agent.Position), 0, 2);
        Assert.Equal(goalNode.Position, agent.Position);
    }

    [Fact]
    public void MoveToExitPointWithinWalkingDistanceToGoal()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.9672284, 53.5573791);
        Assert.InRange(start.DistanceInMTo(goal), 1000, 2000);

        var agent = new TestWalkingGatewayPedestrian
        {
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);

        for (var tick = 0; tick < 10000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep()) agent.Tick();
        Assert.True(agent.GoalReached);

        var gateway = _gatewayLayer.Validate(start, goal).Item2;
        Assert.InRange(gateway.DistanceInMTo(agent.Position), 0, 50);
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(gateway, agent.Position);
    }

    [Fact]
    public void MoveToGatewayHop()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.88361, 53.55891);
        Assert.InRange(start.DistanceInMTo(goal), 4000, 5000);

        var agent = new TestWalkingGatewayPedestrian
        {
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);

        for (var tick = 0; tick < 10000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep()) agent.Tick();
        Assert.True(agent.GoalReached);

        var gateway = _gatewayLayer.Validate(start, goal).Item2;
        Assert.InRange(gateway.DistanceInMTo(goal), 1000, 5000);
        Assert.InRange(gateway.DistanceInMTo(agent.Position), 0, 50);
        Assert.Equal(gateway, agent.Position);
    }
}