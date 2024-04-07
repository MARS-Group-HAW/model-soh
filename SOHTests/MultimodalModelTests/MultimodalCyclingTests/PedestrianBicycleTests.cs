using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Bicycle.Rental;
using SOHModel.Car.Model;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalCyclingTests;

public class PedestrianBicycleTests
{
    private static void CompareCyclistAndPedestrian(TestMultimodalLayer multimodalLayer, Position start,
        Position goal, bool pedestrianArrivesFirst)
    {
        var simulationContext = multimodalLayer.Context;

        var pedestrian = new TestWalkingPedestrian
        {
            StartPosition = start,
            GoalPosition = goal
        };
        pedestrian.Init(multimodalLayer);

        var cyclist = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike
        };
        cyclist.Init(multimodalLayer);

        var agents = new List<MultimodalAgent<TestMultimodalLayer>>
        {
            pedestrian,
            cyclist
        };
        Assert.Equal(ModalChoice.Walking, pedestrian.RouteMainModalChoice);
        Assert.Equal(ModalChoice.CyclingRentalBike, cyclist.RouteMainModalChoice);

        var cyclistArrivedGoal = false;
        for (var tick = 0; tick < 10000 && !agents.All(agent => agent.GoalReached); tick++)
        {
            // foreach (var agent in agents) agent.Tick();
            pedestrian.Tick();
            cyclist.Tick();
            simulationContext.UpdateStep();

            if (!cyclistArrivedGoal && cyclist.GoalReached)
            {
                Assert.Equal(pedestrianArrivesFirst, pedestrian.GoalReached);
                cyclistArrivedGoal = true;
            }
        }

        Assert.True(agents.All(agent => agent.GoalReached));
        Assert.True(agents.All(agent => agent.Whereabouts == Whereabouts.Offside));

        foreach (var agent in agents)
        {
            Assert.Equal(goal.Longitude, agent.Position.Longitude, 2);
            Assert.Equal(goal.Latitude, agent.Position.Latitude, 2);
        }
    }

    private static (Position, Position) FindPedestrianBlockingCyclistTour()
    {
        var start = Position.CreateGeoPosition(9.950500, 53.555871);
        var goal = Position.CreateGeoPosition(9.942655, 53.555227);
        return (start, goal);
    }

    private void BicycleIsReturned(TestMultimodalLayer layer, IBicycleRentalLayer bicycleLayer, Position start,
        Position goal)
    {
        Assert.NotNull(bicycleLayer);
        Assert.Equal(BicycleRentalStation.StandardAmount, bicycleLayer.Nearest(start, false).Count);

        var cyclist = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike
        };
        cyclist.Init(layer);

        var goalBicycleParkingSpace = bicycleLayer.Nearest(goal, false);
        Assert.Equal(BicycleRentalStation.StandardAmount, goalBicycleParkingSpace.Count);

        for (var tick = 0; tick < 10000 && !cyclist.GoalReached; tick++) cyclist.Tick();

        Assert.Equal(BicycleRentalStation.StandardAmount + 1, goalBicycleParkingSpace.Count);
    }

    private void GoalReachedByBicycle(TestMultimodalLayer layer, Position start, Position goal)
    {
        var cyclist = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike
        };
        cyclist.Init(layer);
        for (var tick = 0; tick < 10000 && !cyclist.GoalReached; tick++, layer.Context.UpdateStep()) cyclist.Tick();

        Assert.True(cyclist.GoalReached);

        Assert.True(cyclist.HasUsedBicycle);
        Assert.Equal(goal.Longitude, cyclist.Position.Longitude, 2);
        Assert.Equal(goal.Latitude, cyclist.Position.Latitude, 2);
    }

    [Fact]
    public void BicycleIsReturnedForCycleOnly()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var streetLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(streetLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer)
        {
            BicycleRentalLayer = bicycleLayer
        };

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node3.Position;

        BicycleIsReturned(layer, bicycleLayer, start, goal);
    }

    [Fact]
    public void BicycleIsReturnedForCycleWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var streetLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(streetLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node4.Position;

        BicycleIsReturned(layer, bicycleLayer, start, goal);
    }

    [Fact]
    public void BicycleIsReturnedForWalkCycle()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var streetLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(streetLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node1Pos;
        var goal = FourNodeGraphEnv.Node3Pos;

        BicycleIsReturned(layer, bicycleLayer, start, goal);
    }

    [Fact]
    public void BicycleIsReturnedForWalkCycleWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var streetLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(streetLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node1Pos;
        var goal = FourNodeGraphEnv.Node4Pos;

        BicycleIsReturned(layer, bicycleLayer, start, goal);
    }

    [Fact]
    public void CyclistCannotSurpassPedestrianOnBlockingRoute()
    {
        // we only have one environment for both
        var environment = new SpatialGraphEnvironment(ResourcesConstants.WalkGraphAltonaAltstadt);
        var bicycleLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(environment, bicycleLayer);

        var (start, goal) = FindPedestrianBlockingCyclistTour();
        CompareCyclistAndPedestrian(layer, start, goal, true);
    }

    [Fact]
    public void CyclistCanSurpassPedestrianOnBlockingRoute()
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
                        { Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Cycling } }
                }
            }
        });


        //we have two environments, so cyclist can surpass pedestrian
        var bicycleLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(environment, bicycleLayer);

        var (start, goal) = FindPedestrianBlockingCyclistTour();
        CompareCyclistAndPedestrian(layer, start, goal, false);
    }

    [Fact]
    public void GoalReachedByCycleOnly()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var carLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(carLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node2Pos;
        var goal = FourNodeGraphEnv.Node3Pos;

        GoalReachedByBicycle(layer, start, goal);
    }

    [Fact]
    public void GoalReachedByCycleWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var carLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(carLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node2Pos;
        var goal = FourNodeGraphEnv.Node4Pos;

        GoalReachedByBicycle(layer, start, goal);
    }

    [Fact]
    public void GoalReachedByWalkCycle()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var carLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(carLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node1Pos;
        var goal = FourNodeGraphEnv.Node3Pos;

        GoalReachedByBicycle(layer, start, goal);
    }

    [Fact]
    public void GoalReachedByWalkCycleWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var carLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(carLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node1Pos;
        var goal = FourNodeGraphEnv.Node4Pos;

        GoalReachedByBicycle(layer, start, goal);
    }

    [Fact]
    public void StartIsGoal()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var carLayer = new CarLayer(fourNodeGraphEnv.GraphEnvironment);
        var bicycleLayer = new FourNodeBicycleRentalLayerFixture(carLayer).BicycleRentalLayer;
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment, bicycleLayer);

        var start = FourNodeGraphEnv.Node2Pos;
        var goal = start;

        var cyclist = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike
        };
        cyclist.Init(layer);

        for (var tick = 0; tick < 10000 && !cyclist.GoalReached; tick++) cyclist.Tick();

        Assert.True(cyclist.GoalReached);

        Assert.Equal(goal.Longitude, cyclist.Position.Longitude, 2);
        Assert.Equal(goal.Latitude, cyclist.Position.Latitude, 2);
    }
}