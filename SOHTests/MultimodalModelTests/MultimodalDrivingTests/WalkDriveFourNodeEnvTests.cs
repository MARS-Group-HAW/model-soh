using Mars.Interfaces;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalDrivingTests;

public class WalkDriveFourNodeEnvTests
{
    private static Golf CreateCarOnNode2(FourNodeGraphEnv fourNodeGraphEnv)
    {
        var streetLayer = new StreetLayer { Environment = fourNodeGraphEnv.GraphEnvironment };
        var parkingLayer = new FourNodeCarParkingLayerFixture(streetLayer).CarParkingLayer;
        return Golf.CreateOnParking(parkingLayer, fourNodeGraphEnv.GraphEnvironment,
            fourNodeGraphEnv.Node2.Position);
    }

    private static void StartSimulation(TestMultiCapableCarDriver driver, IParkingCar car,
        ISimulationContext contextImpl)
    {
        var firstParking = car.CarParkingSpace;

        for (var tick = 0; tick < 1000 && !driver.GoalReached; tick++, contextImpl.UpdateStep()) driver.Tick();

        Assert.NotNull(car.CarParkingSpace);
        Assert.NotEqual(car.CarParkingSpace, firstParking);
    }

    [Fact]
    public void Drive()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node3.Position;

        var car = CreateCarOnNode2(fourNodeGraphEnv);
        var driver = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        driver.Init(layer);

        StartSimulation(driver, car, layer.Context);

        Assert.True(driver.HasUsedCar);
        Assert.Equal(goal.Longitude, driver.Position.Longitude, 2);
        Assert.Equal(goal.Latitude, driver.Position.Latitude, 2);
    }

    [Fact]
    public void DriveWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node4.Position;

        var car = CreateCarOnNode2(fourNodeGraphEnv);
        var driver = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        driver.Init(layer);

        StartSimulation(driver, car, layer.Context);

        Assert.True(driver.HasUsedCar);
        Assert.Equal(goal.Longitude, driver.Position.Longitude, 2);
        Assert.Equal(goal.Latitude, driver.Position.Latitude, 2);
    }

    [Fact]
    public void StartIsGoal()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node2.Position;

        var car = CreateCarOnNode2(fourNodeGraphEnv);
        var driver = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        driver.Init(layer);

        for (var tick = 0; tick < 10000 && !driver.GoalReached; tick++) driver.Tick();

        Assert.Equal(goal.Longitude, driver.Position.Longitude, 2);
        Assert.Equal(goal.Latitude, driver.Position.Latitude, 2);
    }

    [Fact]
    public void WalkDrive()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node1.Position;
        var goal = fourNodeGraphEnv.Node3.Position;

        var car = CreateCarOnNode2(fourNodeGraphEnv);
        var driver = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        driver.Init(layer);

        StartSimulation(driver, car, layer.Context);

        Assert.True(driver.HasUsedCar);
        Assert.Equal(goal.Longitude, driver.Position.Longitude, 3);
        Assert.Equal(goal.Latitude, driver.Position.Latitude, 3);
    }

    [Fact]
    public void WalkDriveWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node1.Position;
        var goal = fourNodeGraphEnv.Node4.Position;

        var car = CreateCarOnNode2(fourNodeGraphEnv);
        var driver = new TestMultiCapableCarDriver
        {
            StartPosition = start,
            GoalPosition = goal,
            Car = car
        };
        driver.Init(layer);

        StartSimulation(driver, car, layer.Context);

        Assert.True(driver.HasUsedCar);
        Assert.Equal(goal.Longitude, driver.Position.Longitude, 5);
        Assert.Equal(goal.Latitude, driver.Position.Latitude, 5);
    }
}