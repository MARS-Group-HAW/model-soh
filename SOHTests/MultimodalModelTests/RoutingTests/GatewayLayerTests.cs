using System;
using Mars.Components.Environments;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using SOHMultimodalModel.Routing;
using Xunit;

namespace SOHTests.MultimodalModelTests.RoutingTests;

public class GatewayLayerTests
{
    private readonly SpatialGraphEnvironment _environment;
    private readonly GatewayLayer _gatewayLayer;

    public GatewayLayerTests()
    {
        _environment = new SpatialGraphEnvironment(ResourcesConstants.WalkGraphAltonaAltstadt);
        _gatewayLayer = new GatewayLayer(_environment);
        _gatewayLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig = { File = ResourcesConstants.RailroadStations }
        });
    }

    [Fact]
    public void FindGoalWithinEnv()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467); //Schomburgstr/Hospitalstr
        var goal = Position.CreateGeoPosition(9.936516, 53.547820); //Königstr/Nordelbische Kirchenbib
        // Assert.True(_environment.BoundingBox.Envelope.Contains(start.PositionArray));

        var validatedGoal = _gatewayLayer.Validate(start, goal).Item2;

        Assert.Equal(goal, validatedGoal);

        var startNode = _environment.NearestNode(start);
        var goalNode = _environment.NearestNode(validatedGoal);
        var route = _environment.FindShortestRoute(startNode, goalNode);
        Assert.NotNull(route);
        Assert.NotEmpty(route);
    }

    [Fact]
    public void FindExitPointWithinWalkingDistanceToGoal()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.9672284, 53.5573791);

        var validatedGoal = _gatewayLayer.Validate(start, goal).Item2;

        var expectedGoal = Position.CreateGeoPosition(9.9590506, 53.5585846);

        Assert.Equal(expectedGoal, validatedGoal);
        Assert.InRange(goal.DistanceInKmTo(validatedGoal), 0, 1);
    }

    [Fact]
    public void FindEntryPointWithinWalkingDistanceToStart()
    {
        var start = Position.CreateGeoPosition(9.9672284, 53.5573791);
        var goal = Position.CreateGeoPosition(9.9460806, 53.5525467);

        var validatedGoal = _gatewayLayer.Validate(start, goal).Item1;

        var expectedGoal = Position.CreateGeoPosition(9.9590506, 53.5585846);

        Assert.Equal(expectedGoal, validatedGoal);
        Assert.InRange(start.DistanceInKmTo(validatedGoal), 0, 1);
    }

    [Fact]
    public void FindExitPointOverGateway()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.88361, 53.55891);

        var gatewayPosition = _gatewayLayer.Validate(start, goal).Item2;

        var railStation = Position.CreateGeoPosition(9.944125, 53.547752);
        var railStationNodePosition = _environment.NearestNode(railStation).Position;

        Assert.InRange(goal.DistanceInKmTo(gatewayPosition), 1, 10);
        Assert.Equal(railStationNodePosition, gatewayPosition);
    }

    [Fact]
    public void FindEntryPointOverGateway()
    {
        var start = Position.CreateGeoPosition(9.88361, 53.55891); //S-Bahn Othmarschen
        var goal = Position.CreateGeoPosition(9.9460806, 53.5525467); //Schomburgstr/Hospitalstr

        var gatewayPosition = _gatewayLayer.Validate(start, goal).Item1;

        var railStation = Position.CreateGeoPosition(9.944125, 53.547752); //S-Bahn Königstr
        var railStationNodePosition = _environment.NearestNode(railStation).Position;

        Assert.InRange(start.DistanceInKmTo(gatewayPosition), 1, 10);
        Assert.Equal(railStationNodePosition, gatewayPosition);
    }

    [Fact]
    public void StartAndGoalOutsideEnvironment()
    {
        var start = Position.CreateGeoPosition(9.9675872, 53.5614485);
        var goal = Position.CreateGeoPosition(9.9672284, 53.5573791);

        var validatedGoal = _gatewayLayer.Validate(start, goal).Item2;

        Assert.Equal(goal, validatedGoal);
    }

    [Fact]
    public void GoalFarOutsideEnvironmentButLayerNotInitialized()
    {
        var start = Position.CreateGeoPosition(9.9460806, 53.5525467);
        var goal = Position.CreateGeoPosition(9.88361, 53.55891);

        var environment = new SpatialGraphEnvironment(ResourcesConstants.WalkGraphAltonaAltstadt);
        var gatewayLayer = new GatewayLayer(environment);
        Assert.Throws<ApplicationException>(() => gatewayLayer.Validate(start, goal));
    }
}