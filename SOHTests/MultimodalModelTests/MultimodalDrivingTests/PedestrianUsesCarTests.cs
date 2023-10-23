using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHCarModel.Model;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalDrivingTests;

public class PedestrianUsesCarTests
{
    [Fact]
    public void DriverEntersCarWithDifferentRanges()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);
        var environment = fourNodeGraphEnv.GraphEnvironment;
        var driver = new TestDrivingPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        driver.Init(multimodalLayer);
        Assert.Equal(FourNodeGraphEnv.Node1Pos, driver.Position);

        var carOnNode1 = Golf.Create(fourNodeGraphEnv.GraphEnvironment);
        Assert.Null(carOnNode1.Position);
        Assert.True(environment.Insert(carOnNode1, fourNodeGraphEnv.Node1));
        Assert.Equal(FourNodeGraphEnv.Node1Pos, carOnNode1.Position);

        Assert.True(driver.TryEnterVehicleAsDriver(carOnNode1, driver));
        Assert.Equal(FourNodeGraphEnv.Node1Pos, driver.Position);
        Assert.True(driver.TryLeaveVehicle(driver));
        Assert.Equal(FourNodeGraphEnv.Node1Pos, driver.Position);

        var carOnNode2 = Golf.Create(fourNodeGraphEnv.GraphEnvironment);
        carOnNode2.Position = FourNodeGraphEnv.Node2Pos;
        Assert.Equal(FourNodeGraphEnv.Node2Pos, carOnNode2.Position);
        Assert.True(environment.Insert(carOnNode2, fourNodeGraphEnv.Node2));

        // range check should fail, therefore tryEnter returns false
        Assert.False(driver.TryEnterVehicleAsDriver(carOnNode2, driver));
    }

    [Fact]
    public void PedestrianDrivesCarTest()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);
        var environment = fourNodeGraphEnv.GraphEnvironment;

        var car = Golf.Create(fourNodeGraphEnv.GraphEnvironment);
        Assert.True(environment.Insert(car, fourNodeGraphEnv.Node1));
        Assert.Equal(car.Position, FourNodeGraphEnv.Node1Pos);

        var drivingPedestrian = new TestDrivingPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos,
            Car = car
        };
        drivingPedestrian.Init(multimodalLayer);
        Assert.Equal(drivingPedestrian.Position, FourNodeGraphEnv.Node1Pos);

        Assert.False(fourNodeGraphEnv.Node2.Position.DistanceInMTo(drivingPedestrian.Position) < 5d);

        var route = environment.FindRoute(fourNodeGraphEnv.Node1, fourNodeGraphEnv.Node2);
        Assert.NotEmpty(route);
        drivingPedestrian.MultimodalRoute = new MultimodalRoute(new List<RouteStop>
            { new(route, ModalChoice.CarDriving) });
        Assert.True(drivingPedestrian.TryEnterVehicleAsDriver(car, drivingPedestrian));

        for (var tick = 0; tick < 5000 && !drivingPedestrian.GoalReached; tick++)
        {
            drivingPedestrian.Tick();
            Assert.Equal(car.Position, drivingPedestrian.Position);
        }

        Assert.InRange(FourNodeGraphEnv.Node2Pos.DistanceInMTo(drivingPedestrian.Position), 0, 11d);

        drivingPedestrian.TryLeaveVehicle(drivingPedestrian);
        Assert.InRange(FourNodeGraphEnv.Node2Pos.DistanceInMTo(drivingPedestrian.Position), 0, 700d);
    }

    [Fact]
    public void WalkToNodeDriveToNodeWalkToNodeTest()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var multimodalLayer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);
        var environment = fourNodeGraphEnv.GraphEnvironment;
        var driver = new TestDrivingPedestrian
        {
            StartPosition = FourNodeGraphEnv.Node1Pos
        };
        driver.Init(multimodalLayer);
        Assert.Equal(FourNodeGraphEnv.Node1Pos, driver.Position);

        driver.Move(); // nothing changes
        Assert.Equal(FourNodeGraphEnv.Node1Pos, driver.Position);

        var car = Golf.Create(environment);
        car.Position = fourNodeGraphEnv.Node2.Position;
        var carInserted = environment.Insert(car, fourNodeGraphEnv.Node2);
        Assert.True(carInserted);

        var myRoute = environment.FindRoute(fourNodeGraphEnv.Node1, fourNodeGraphEnv.Node2);
        driver.MultimodalRoute = new MultimodalRoute(myRoute, ModalChoice.Walking);

        Assert.Equal(fourNodeGraphEnv.Node1.Position, driver.Position);
        for (var tick = 0; tick < 5000 && !driver.GoalReached; tick++) driver.Tick();

        Assert.InRange(driver.Position.DistanceInMTo(fourNodeGraphEnv.Node2.Position), 0, 1);
        Assert.InRange(driver.Position.DistanceInMTo(car.Position), 0, 1);

        var route = environment.FindRoute(fourNodeGraphEnv.Node2, fourNodeGraphEnv.Node3);
        Assert.True(driver.TryEnterVehicleAsDriver(car, driver));
        driver.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);

        for (var tick = 0; tick < 5000 && !driver.GoalReached; tick++) driver.Tick();

        driver.TryLeaveVehicle(driver);

        Assert.InRange(FourNodeGraphEnv.Node3Pos.DistanceInMTo(driver.Position), 0, 10);
    }
}