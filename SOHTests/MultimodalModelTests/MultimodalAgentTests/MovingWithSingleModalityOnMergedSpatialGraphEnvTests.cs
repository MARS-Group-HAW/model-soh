using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using Moq;
using NetTopologySuite.Geometries;
using SOHBicycleModel.Rental;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHDomain.Graph;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalAgentTests;

public class MovingWithSingleModalityOnMergedSpatialGraphEnvTests
{
    private readonly SpatialGraphOptions _options;

    public MovingWithSingleModalityOnMergedSpatialGraphEnvTests()
    {
        _options = new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.TriangleNetwork,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Cycling },
                        NodeToleranceInMeter = 20f
                    }
                },
                new()
                {
                    File = ResourcesConstants.TriangleNetwork,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking },
                        NodeToleranceInMeter = 20f
                    }
                },
                new()
                {
                    File = ResourcesConstants.TriangleNetwork,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving },
                        NodeToleranceInMeter = 20f
                    }
                }
            }
        };
    }

    [Fact]
    public void GraphEnvironmentHoldsLaneForWalkingCyclingCarDriving()
    {
        var environment = new SpatialGraphEnvironment(_options);

        Assert.Equal(3, environment.Nodes.Count);
        Assert.Equal(3, environment.Edges.Count);
        var edge = environment.Edges.Values.First();
        Assert.Equal(3, edge.LaneCount);
        Assert.IsType<SpatialEdge>(edge);
        var spatialEdge = (SpatialEdge)edge;

        Assert.Contains(SpatialModalityType.Cycling, spatialEdge.Modalities);
        Assert.Contains(SpatialModalityType.Walking, spatialEdge.Modalities);
        Assert.Contains(SpatialModalityType.CarDriving, spatialEdge.Modalities);

        Assert.All(spatialEdge.SpatialLanes, lane => Assert.Single(lane.Modalities));
    }

    [Fact]
    public void WalkOnWalkingLane()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var start = environment.Nodes.First();
        var goal = environment.Nodes.Last();

        var (minLane, maxLane) = environment.Edges.First().Value.ModalityLaneRanges[SpatialModalityType.Walking];

        var layer = new TestMultimodalLayer(environment);
        var agent = new TestWalkingPedestrian
        {
            StartPosition = start.Position,
            GoalPosition = goal.Position
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(start.Position, agent.Position);

        Assert.NotEmpty(agent.MultimodalRoute);
        Assert.True(agent.MultimodalRoute.RouteLength > 0);
        const int ticks = 1000;
        for (var tick = 0; tick < ticks && !agent.GoalReached; tick++)
        {
            agent.Tick();
            Assert.InRange(agent.WalkingShoes.LaneOnCurrentEdge, minLane, maxLane);
        }

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(goal.Position, agent.Position);
    }

    [Fact]
    public void DriveOnDrivingLane()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var start = environment.Nodes.First();
        var goal = environment.Nodes.Last();

        var (minLane, maxLane) = environment.Edges.First().Value.ModalityLaneRanges[SpatialModalityType.CarDriving];

        var carParkingLayer = CreateParkingLayer(environment);
        var car = carParkingLayer.CreateOwnCarNear(start.Position);

        var layer = new TestMultimodalLayer(environment)
        {
            CarParkingLayer = carParkingLayer
        };
        var agent = new TestMultiCapableAgent
        {
            StartPosition = start.Position,
            GoalPosition = goal.Position,
            ModalChoice = ModalChoice.CarDriving,
            CarParkingLayer = carParkingLayer,
            Car = car
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(start.Position, agent.Position);
        Assert.Equal(ModalChoice.CarDriving, agent.RouteMainModalChoice);

        Assert.NotEmpty(agent.MultimodalRoute);
        Assert.True(agent.MultimodalRoute.RouteLength > 0);

        Assert.False(agent.GoalReached);
        const int ticks = 1000;
        for (var tick = 0; tick < ticks && !agent.GoalReached; tick++)
        {
            agent.Tick();
            Assert.InRange(car.LaneOnCurrentEdge, minLane, maxLane);
        }

        Assert.True(agent.GoalReached);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(goal.Position, agent.Position);
    }

    private static CarParkingLayer CreateParkingLayer(ISpatialGraphEnvironment environment)
    {
        var features = environment.Nodes.Select(node => new VectorStructuredData
        {
            Data = new Dictionary<string, object> { { "area", 0 } },
            Geometry = new Point(node.Position.X, node.Position.Y)
        });
        var dataTable = new CsvReader(ResourcesConstants.CarCsv, true).ToTable();
        var entityManagerImpl = new EntityManagerImpl((typeof(Car), dataTable));
        var mock = new Mock<ISimulationContainer>();

        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(entityManagerImpl);
        var mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features
            },
            Container = mock.Object
        };

        var carParkingLayer = new CarParkingLayer { StreetLayer = new StreetLayer { Environment = environment } };
        carParkingLayer.InitLayer(mapping);
        return carParkingLayer;
    }


    [Fact(Skip = "TODO")]
    public void CycleOnCyclingLane()
    {
        var environment = new SpatialGraphEnvironment(_options);
        var start = environment.Nodes.First();
        var goal = environment.Nodes.Last();


        // var (minLane, maxLane) = environment.Edges.First().Value.ModalityLaneRanges[SpatialModalityType.Cycling];

        var bicycleRentalLayer = CreateRentalCyclingLayer(environment);

        var layer = new TestMultimodalLayer(environment)
        {
            BicycleRentalLayer = bicycleRentalLayer
        };
        var agent = new TestMultiCapableAgent
        {
            StartPosition = start.Position,
            GoalPosition = goal.Position,
            ModalChoice = ModalChoice.CyclingRentalBike,
            BicycleRentalLayer = bicycleRentalLayer
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(start.Position, agent.Position);
        Assert.Equal(ModalChoice.CyclingRentalBike, agent.RouteMainModalChoice);

        Assert.NotEmpty(agent.MultimodalRoute);
        Assert.True(agent.MultimodalRoute.RouteLength > 0);

        Assert.False(agent.GoalReached);
        const int ticks = 1000;
        for (var tick = 0; tick < ticks && !agent.GoalReached; tick++) agent.Tick();

        Assert.True(agent.GoalReached);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.Equal(goal.Position, agent.Position);
    }

    private static BicycleRentalLayer CreateRentalCyclingLayer(ISpatialGraphEnvironment environment)
    {
        var features = environment.Nodes.Select(node => new VectorStructuredData
        {
            Data = new Dictionary<string, object> { { "Anzahl", 10 }, { "name", node.Index } },
            Geometry = new Point(node.Position.X, node.Position.Y)
        });
        var dataTable = new CsvReader(ResourcesConstants.BicycleCsv, true).ToTable();
        var entityManagerImpl = new EntityManagerImpl((typeof(RentalBicycle), dataTable));
        var mock = new Mock<ISimulationContainer>();

        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(entityManagerImpl);
        var mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features
            },
            Container = mock.Object
        };

        var rentalLayer = new BicycleRentalLayer
        {
            SpatialGraphMediatorLayer = new SpatialGraphMediatorLayer
            {
                Environment = environment
            }
        };
        rentalLayer.InitLayer(mapping);
        return rentalLayer;
    }
}