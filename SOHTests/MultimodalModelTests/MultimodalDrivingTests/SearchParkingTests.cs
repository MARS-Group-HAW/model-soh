using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHDomain.Graph;
using SOHMultimodalModel.Multimodal;
using SOHMultimodalModel.Routing;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalDrivingTests;

/// <summary>
///     It should be checked if a car searches for other parking spots if the one that was chosen in the routing process is
///     occupied.
///     Therefore the agent start at Node4 and moves back to Node1
/// </summary>
public class SearchParkingTests
{
    private static (Position, Position) FindReasonableRoute(SpatialGraphEnvironment environment)
    {
        var start = environment.GetRandomNode(SpatialModalityType.Walking).Position;
        var goal = environment.GetRandomNode(SpatialModalityType.Walking).Position;
        var counter = 0;
        while (goal.DistanceInKmTo(start) < 0.5 &&
               environment.FindShortestRoute(environment.NearestNode(start), environment.NearestNode(goal)) == null)
        {
            goal = environment.GetRandomNode().Position;
            if (counter++ > 1000)
                throw new ApplicationException("Could not find two points that are far enough from each other");
        }

        return (start, goal);
    }

    [Fact]
    public void ParkingIsUsedOnArrivalOtherParkingOnSameNode()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = FourNodeGraphEnv.Node4Pos;
        var goal = FourNodeGraphEnv.Node1Pos;
        var streetLayer = new StreetLayer { Environment = fourNodeGraphEnv.GraphEnvironment };
        var parkingLayer = new FourNodeCarParkingLayerFixture(streetLayer).CarParkingLayer;
        var car = Golf.CreateOnParking(parkingLayer, fourNodeGraphEnv.GraphEnvironment, FourNodeGraphEnv.Node3Pos);

        var agent = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        agent.Init(layer);
        var firstParking = car.CarParkingSpace;

        // occupy desired parking spot, others near that node remain free
        var nearestParking = car.CarParkingLayer.Nearest(FourNodeGraphEnv.Node2Pos);
        Assert.True(nearestParking.HasCapacity);
        Golf.CreateOnParking(parkingLayer, fourNodeGraphEnv.GraphEnvironment, FourNodeGraphEnv.Node2Pos);
        Assert.False(nearestParking.HasCapacity);

        var secondNearestParking = car.CarParkingLayer.Nearest(FourNodeGraphEnv.Node2Pos);
        Assert.True(secondNearestParking.HasCapacity);

        for (var tick = 0; tick < 1000 && !agent.GoalReached; tick++, layer.Context.UpdateStep())
        {
            agent.Tick();
            if (agent.HasUsedCar && agent.Whereabouts == Whereabouts.Sidewalk)
            {
                var distanceToStartParking = agent.Position.DistanceInMTo(FourNodeGraphEnv.Node3Pos);
                var distanceToGoalParking = agent.Position.DistanceInMTo(FourNodeGraphEnv.Node2Pos);
                Assert.InRange(distanceToGoalParking, 0, distanceToStartParking);
            }
        }
        //should still park on Node2, there are plenty of other parking spots

        Assert.NotNull(car.CarParkingSpace);
        Assert.NotEqual(car.CarParkingSpace, firstParking);
        Assert.Equal(secondNearestParking, car.CarParkingSpace);

        Assert.True(agent.HasUsedCar);
        Assert.Equal(goal.Longitude, agent.Position.Longitude, 5);
        Assert.Equal(goal.Latitude, agent.Position.Latitude, 5);
    }

    [Fact]
    public void ParkingSpotIsOccupiedOnArrivalDriveBackAltonaAltstadt()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.WalkGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                        { Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking } }
                },
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                        { Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving } }
                }
            }
        });

        var parkingLayer = new CarParkingLayerFixture(new StreetLayer { Environment = environment }).CarParkingLayer;

        var layer = new TestMultimodalLayer(environment)
        {
            CarParkingLayer = parkingLayer
        };

        // specific problematic route for testing
        // var start = Position.CreatePosition(9.9517071, 53.5575623);
        // var goal = Position.CreatePosition(9.9517643, 53.5517866);

        var (start, goal) = FindReasonableRoute(environment);
        var car = Golf.CreateOnParking(parkingLayer, environment, start);
        var agent = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        agent.Init(layer);
        var firstParking = car.CarParkingSpace;

        // occupy all parking spots but the starting one
        foreach (var space in parkingLayer.Features.OfType<CarParkingSpace>())
            space.Occupied = !space.Equals(firstParking);

        //agent does not know and drives to desired parking
        for (var tick = 0; tick < 6000 && !agent.GoalReached; tick++, layer.Context.UpdateStep())
            agent.Tick();

        Assert.True(agent.GoalReached);

        Assert.NotNull(car.CarParkingSpace);
        Assert.Equal(car.CarParkingSpace, firstParking);

        Assert.True(agent.HasUsedCar);
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.InRange(agent.Position.DistanceInMTo(goal), 0, 20);
    }

    [Fact]
    public void ParkingSpotIsOccupiedOnArrivalDriveBackFourNodeEnv()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = FourNodeGraphEnv.Node4Pos;
        var goal = FourNodeGraphEnv.Node1Pos;
        var streetLayer = new StreetLayer { Environment = fourNodeGraphEnv.GraphEnvironment };
        var parkingLayer = new FourNodeCarParkingLayerFixture(streetLayer).CarParkingLayer;
        var car = Golf.CreateOnParking(parkingLayer, fourNodeGraphEnv.GraphEnvironment, FourNodeGraphEnv.Node3Pos);

        var agent = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        agent.Init(layer);
        Assert.Equal(ModalChoice.CarDriving, agent.MultimodalRoute.MainModalChoice);

        var firstParking = car.CarParkingSpace;

        // occupy all parking spots but the starting one
        foreach (var space in parkingLayer.Explore(goal.PositionArray, 3000))
            space.Occupied = !space.Equals(firstParking);

        //agent does not know and drives to desired parking
        for (var tick = 0; tick < 2000 && !agent.GoalReached; tick++, layer.Context.UpdateStep()) agent.Tick();

        // on arrival (Node2) recognizes that all are occupied
        // drives back to Node3, because there is a free parking spot

        Assert.True(agent.GoalReached);
        Assert.All(MultimodalRouteCommons.GiveDistanceOfSwitchPoints(agent.MultimodalRoute),
            d => Assert.InRange(d, 0, 10));

        Assert.NotNull(car.CarParkingSpace);
        Assert.Equal(car.CarParkingSpace, firstParking);

        Assert.True(agent.HasUsedCar);
        Assert.InRange(agent.Position.DistanceInMTo(goal), 0, 5);
        Assert.Equal(goal.Longitude, agent.Position.Longitude, 5);
        Assert.Equal(goal.Latitude, agent.Position.Latitude, 5);
    }

    [Fact]
    public void ParkingStaysFreeHappyPath()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = FourNodeGraphEnv.Node4Pos;
        var goal = FourNodeGraphEnv.Node1Pos;
        var streetLayer = new StreetLayer { Environment = fourNodeGraphEnv.GraphEnvironment };
        var parkingLayer = new FourNodeCarParkingLayerFixture(streetLayer).CarParkingLayer;
        var car = Golf.CreateOnParking(parkingLayer, fourNodeGraphEnv.GraphEnvironment, FourNodeGraphEnv.Node3Pos);

        var agent = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        agent.Init(layer);
        var firstParking = car.CarParkingSpace;

        for (var tick = 0;
             tick < 1000 && !agent.GoalReached;
             tick++, layer.Context.UpdateStep()) agent.Tick();

        Assert.NotNull(car.CarParkingSpace);
        Assert.NotEqual(car.CarParkingSpace, firstParking);

        Assert.True(agent.HasUsedCar);
        Assert.Equal(goal.Longitude, agent.Position.Longitude, 5);
        Assert.Equal(goal.Latitude, agent.Position.Latitude, 5);
    }
}