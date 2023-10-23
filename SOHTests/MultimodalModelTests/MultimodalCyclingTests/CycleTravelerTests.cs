using System.Collections.Generic;
using Mars.Common;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using SOHBicycleModel.Rental;
using SOHDomain.Graph;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Routing;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalCyclingTests;

public class CycleTravelerTests
{
    private readonly GatewayLayer _gatewayLayer;
    private readonly CycleTravelerLayer _layer;
    private readonly ISpatialGraphEnvironment _sideWalk;

    public CycleTravelerTests()
    {
        var spatialGraphLayer = new SpatialGraphMediatorLayer();
        spatialGraphLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig =
            {
                Inputs = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.DriveGraphAltonaAltstadt,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                            Modalities = new HashSet<SpatialModalityType>
                            {
                                SpatialModalityType.Walking,
                                SpatialModalityType.Cycling
                            }
                        }
                    }
                }
            }
        });

        var bicycleRentalLayer = new BicycleRentalLayer
        {
            SpatialGraphMediatorLayer = new SpatialGraphMediatorLayer
            {
                Environment = spatialGraphLayer.Environment
            }
        };

        bicycleRentalLayer.InitLayer(new LayerInitData
            {
                LayerInitConfig =
                {
                    File = ResourcesConstants.BicycleRentalAltonaAltstadt
                }
            }, Handle, Handle
        );


        _layer = new CycleTravelerLayer
        {
            SpatialGraphMediatorLayer = spatialGraphLayer,
            BicycleRentalLayer = bicycleRentalLayer
        };

        _layer.InitLayer(new LayerInitData(SimulationContext.Start2020InSeconds), Handle, Handle);
        _sideWalk = _layer.SpatialGraphMediatorLayer.Environment;

        _gatewayLayer = new GatewayLayer(_sideWalk);
        _gatewayLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig = { File = ResourcesConstants.RailroadStations }
        });
        _layer.GatewayLayer = _gatewayLayer;
    }

    [Fact]
    public void MoveFromOutsideToInsideOverBorder()
    {
        var start = Position.CreateGeoPosition(9.963353372, 53.554247021);
        var goal = Position.CreateGeoPosition(9.955426, 53.5550917);

        Assert.False(_sideWalk.BoundingBox.Contains(start.ToCoordinate()));
        Assert.True(_sideWalk.BoundingBox.Contains(goal.ToCoordinate()));

        var (validatedStart, validatedGoal) = _gatewayLayer.Validate(start, goal);
        Assert.NotEqual(start, validatedStart);
        Assert.Equal(_sideWalk.NearestNode(start).Position, validatedStart);
        Assert.Equal(goal, validatedGoal);

        var agent = new CycleTraveler
        {
            HasBike = false,
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);
        var visitedStartNode = false;
        for (var tick = 0; tick < 5000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            agent.Tick();
            visitedStartNode |= agent.Position.DistanceInMTo(validatedStart) < 2;
        }

        Assert.True(agent.GoalReached);
        Assert.True(visitedStartNode);
        Assert.Equal(agent.Position, validatedGoal);
    }

    [Fact]
    public void MoveFromInsideToOutsideOverBorder()
    {
        var start = Position.CreateGeoPosition(9.955426, 53.5550917);
        var goal = Position.CreateGeoPosition(9.963353372, 53.554247021);

        Assert.True(_sideWalk.BoundingBox.Contains(start.ToCoordinate()));
        Assert.False(_sideWalk.BoundingBox.Contains(goal.ToCoordinate()));

        var (validatedStart, validatedGoal) = _gatewayLayer.Validate(start, goal);
        Assert.Equal(start, validatedStart);
        Assert.Equal(_sideWalk.NearestNode(goal).Position, validatedGoal);
        Assert.NotEqual(goal, validatedGoal);

        var agent = new CycleTraveler
        {
            HasBike = false,
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);
        var visitedGoalNode = false;
        for (var tick = 0; tick < 5000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            agent.Tick();
            visitedGoalNode |= agent.Position.DistanceInMTo(validatedStart) < 2;
        }

        Assert.True(agent.GoalReached);
        Assert.True(visitedGoalNode);
        Assert.Equal(agent.Position, validatedGoal);
    }

    [Fact]
    public void MoveFromOutsideToInsideOverGateway()
    {
        var start = Position.CreateGeoPosition(9.98491, 53.54944);
        var goal = Position.CreateGeoPosition(9.955426, 53.5550917);

        Assert.False(_sideWalk.BoundingBox.Contains(start.ToCoordinate()));
        Assert.True(_sideWalk.BoundingBox.Contains(goal.ToCoordinate()));

        var (validatedStart, validatedGoal) = _gatewayLayer.Validate(start, goal);
        Assert.NotEqual(start, validatedStart);
        Assert.NotEqual(_sideWalk.NearestNode(start).Position, validatedStart);
        Assert.Equal(goal, validatedGoal);

        var agent = new CycleTraveler
        {
            HasBike = false,
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);
        var visitedStartNode = false;
        for (var tick = 0; tick < 5000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            agent.Tick();
            visitedStartNode |= agent.Position.DistanceInMTo(validatedStart) < 2;
        }

        Assert.True(agent.GoalReached);
        Assert.True(visitedStartNode);
        Assert.Equal(agent.Position, validatedGoal);
    }

    [Fact]
    public void MoveFromInsideToOutsideOverGateway()
    {
        var start = Position.CreateGeoPosition(9.955426, 53.5550917);
        var goal = Position.CreateGeoPosition(9.98491, 53.54944);

        Assert.True(_sideWalk.BoundingBox.Contains(start.ToCoordinate()));
        Assert.False(_sideWalk.BoundingBox.Contains(goal.ToCoordinate()));

        var (validatedStart, validatedGoal) = _gatewayLayer.Validate(start, goal);
        Assert.Equal(start, validatedStart);
        Assert.NotEqual(goal, validatedGoal);

        var agent = new CycleTraveler
        {
            HasBike = false,
            StartPosition = start,
            GoalPosition = goal
        };
        agent.Init(_layer);
        var visitedGoalNode = false;
        for (var tick = 0; tick < 5000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            agent.Tick();
            visitedGoalNode |= agent.Position.DistanceInMTo(validatedGoal) < 2;
        }

        Assert.True(agent.GoalReached);
        Assert.True(visitedGoalNode);
        Assert.Equal(agent.Position, validatedGoal);
    }

    private static void Handle(ILayer layer, ITickClient tickclient)
    {
        //do nothing
    }
}