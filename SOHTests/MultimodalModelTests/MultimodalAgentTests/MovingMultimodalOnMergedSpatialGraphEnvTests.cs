using System.Collections.Generic;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHDomain.Graph;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalAgentTests;

public class MovingMultimodalOnMergedSpatialGraphEnvTests
{
    private readonly SpatialGraphOptions _options;

    public MovingMultimodalOnMergedSpatialGraphEnvTests()
    {
        _options = new SpatialGraphOptions
        {
            // NetworkMerge = true,
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Cycling },
                        IsBiDirectedImport = true
                    }
                },
                new()
                {
                    File = ResourcesConstants.WalkGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking },
                        IsBiDirectedImport = true
                    }
                },
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving },
                        IsBiDirectedImport = true
                    }
                }
            }
        };
    }

    [Fact]
    public void AllRentalStationNodesCanReachEachOther()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var bicycleRentalLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;

        foreach (var start in bicycleRentalLayer.Features)
        foreach (var target in bicycleRentalLayer.Features)
        {
            if (start == target) continue;

            var startPoint = start.VectorStructured.Geometry.Centroid;
            var startNode = environment.NearestNode(Position.CreateGeoPosition(startPoint.X, startPoint.Y));
            var targetPoint = target.VectorStructured.Geometry.Centroid;
            var targetNode = environment.NearestNode(Position.CreateGeoPosition(targetPoint.X, targetPoint.Y));
            var route = environment.FindRoute(startNode, targetNode);
            Assert.NotNull(route);
        }
    }

    [Fact]
    public void DriveOnDrivingLane()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var bicycleRentalLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;
        var carParkingLayer = new CarParkingLayerFixture(new StreetLayer { Environment = environment })
            .CarParkingLayer;

        var start = Position.CreateGeoPosition(9.9546178, 53.557155);
        var goal = Position.CreateGeoPosition(9.9418041, 53.5480482);

        var car = carParkingLayer.CreateOwnCarNear(start);

        var layer = new TestMultimodalLayer(environment)
        {
            CarParkingLayer = carParkingLayer,
            BicycleRentalLayer = bicycleRentalLayer
        };
        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CarDriving,
            CarParkingLayer = carParkingLayer,
            BicycleRentalLayer = bicycleRentalLayer,
            Car = car
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(start, agent.Position);
        Assert.Equal(ModalChoice.CarDriving, agent.RouteMainModalChoice);

        Assert.NotEmpty(agent.MultimodalRoute);
        Assert.True(agent.MultimodalRoute.RouteLength > 0);

        Assert.False(agent.GoalReached);
        const int ticks = 5000;
        for (var tick = 0; tick < ticks && !agent.GoalReached; tick++)
        {
            agent.Tick();

            var edge = agent.Car.CurrentEdge;
            if (edge?.Modalities.Contains(SpatialModalityType.CarDriving) ?? false)
            {
                var (minLane, maxLane) = edge.ModalityLaneRanges[SpatialModalityType.CarDriving];
                if (agent.CurrentlyCarDriving)
                    Assert.InRange(agent.Car.LaneOnCurrentEdge, minLane, maxLane);
                else
                    Assert.NotInRange(agent.Car.LaneOnCurrentEdge, minLane, maxLane);
            }
        }

        agent.Tick();
        Assert.True(agent.GoalReached);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(goal, agent.Position);
    }

    [Fact]
    public void CycleOnCyclingLane()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var bicycleRentalLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;

        var start = Position.CreateGeoPosition(9.9546178, 53.557155);
        var goal = Position.CreateGeoPosition(9.9418041, 53.5480482);

        var layer = new TestMultimodalLayer(environment)
        {
            BicycleRentalLayer = bicycleRentalLayer
        };
        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike,
            BicycleRentalLayer = bicycleRentalLayer
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(start, agent.Position);
        Assert.Equal(ModalChoice.CyclingRentalBike, agent.RouteMainModalChoice);

        var route = agent.MultimodalRoute;
        Assert.NotEmpty(route);
        Assert.True(route.RouteLength > 0);
        Assert.Equal(3, route.Count);

        Assert.False(agent.GoalReached);
        const int ticks = 5000;

        for (var tick = 0; tick < ticks; tick++)
        {
            agent.Tick();

            if (agent.RentalBicycle == null) continue;

            var edge = agent.RentalBicycle.CurrentEdge;
            if (edge?.Modalities.Contains(SpatialModalityType.Cycling) ?? false)
            {
                var (minLane, maxLane) = edge.ModalityLaneRanges[SpatialModalityType.Cycling];
                Assert.InRange(agent.RentalBicycle.LaneOnCurrentEdge, minLane, maxLane);
            }
        }

        Assert.True(agent.GoalReached);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.InRange(goal.DistanceInMTo(agent.Position), 0, 1);
        Assert.Equal(goal, agent.Position);
    }
}