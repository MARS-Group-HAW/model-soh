using Mars.Interfaces.Environments;
using SOHDomain.Graph;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using SOHTests.DomainTests.VehicleTests;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalAgentTests;

public class MultimodalAgentTests
{
    private readonly TestMultiCapableAgent _agent;
    private readonly TestMultimodalLayer _layer;
    private readonly Position _start, _goal;

    public MultimodalAgentTests()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        _start = FourNodeGraphEnv.Node1Pos;
        _goal = FourNodeGraphEnv.Node4Pos;

        var streetLayer = new StreetLayer
        {
            Environment = fourNodeGraphEnv.GraphEnvironment
        };
        _layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment)
        {
            BicycleRentalLayer = new FourNodeBicycleRentalLayerFixture(streetLayer).BicycleRentalLayer,
            CarParkingLayer = new FourNodeCarParkingLayerFixture(streetLayer).CarParkingLayer
        };

        _agent = new TestMultiCapableAgent
        {
            StartPosition = _start,
            CapabilityDrivingOwnCar = true
        };
        _agent.Init(_layer);
    }

    [Fact]
    public void CycleToGoal()
    {
        var route = _layer.Search(_agent, _start, _goal, ModalChoice.CyclingRentalBike);
        _agent.MultimodalRoute = route;
        Assert.Equal(ModalChoice.CyclingRentalBike, _agent.RouteMainModalChoice);

        for (var tick = 0; tick < 500 && !_agent.GoalReached; tick++, _layer.Context.UpdateStep()) _agent.Tick();

        Assert.True(_agent.GoalReached);
        Assert.Equal(Whereabouts.Offside, _agent.Whereabouts);
        Assert.InRange(_agent.Position.DistanceInMTo(route.Goal), 0, 1);
        Assert.Equal(_agent.Position, route.Goal);
        Assert.True(_agent.HasUsedBicycle);
    }

    [Fact]
    public void DriveToGoal()
    {
        var route = _layer.Search(_agent, _start, _goal, ModalChoice.CarDriving);
        _agent.MultimodalRoute = route;

        for (var tick = 0; tick < 10000 && !_agent.GoalReached; tick++, _layer.Context.UpdateStep()) _agent.Tick();

        Assert.True(_agent.GoalReached);
        Assert.Equal(_agent.Position, route.Goal);
        Assert.True(_agent.HasUsedCar);
    }

    [Fact]
    public void DrivingLeavesParkingAndEntersGoalParkingAgain()
    {
        var route = _layer.Search(_agent, _start, _goal, ModalChoice.CarDriving);
        _agent.MultimodalRoute = route;

        var startCarParkingSpace = _agent.Car.CarParkingSpace;
        Assert.NotNull(startCarParkingSpace);
        Assert.Contains(_agent.Car, startCarParkingSpace.ParkingVehicles.Keys);

        var goalCarParkingSpace = _agent.Car.CarParkingLayer.Nearest(_goal);
        Assert.Empty(goalCarParkingSpace.ParkingVehicles);

        var wasDriving = false;
        for (var tick = 0; tick < 10000 && !_agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            _agent.Tick();
            if (!wasDriving && _agent.Whereabouts == Whereabouts.Vehicle)
            {
                wasDriving = true;
                Assert.Null(_agent.Car.CarParkingSpace);
            }
        }

        Assert.True(_agent.GoalReached);
        Assert.True(_agent.HasUsedCar);

        Assert.NotNull(_agent.Car.CarParkingSpace);
        Assert.Equal(goalCarParkingSpace, _agent.Car.CarParkingSpace);
        Assert.Contains(_agent.Car, goalCarParkingSpace.ParkingVehicles.Keys);
    }

    [Fact]
    public void SwitchBetweenAllWhereabouts()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var environment = fourNodeGraphEnv.GraphEnvironment;

        _agent.StartPosition = FourNodeGraphEnv.Node1Pos;

        Assert.Equal(Whereabouts.Offside, _agent.Whereabouts);

        var route = environment.FindRoute(fourNodeGraphEnv.Node1, fourNodeGraphEnv.Node2);
        _agent.MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
        _agent.Move();
        Assert.Equal(Whereabouts.Sidewalk, _agent.Whereabouts);

        var car = new TestCarWithoutRangeCheck(environment);
        Assert.True(environment.Insert(car, fourNodeGraphEnv.Node1));
        Assert.True(_agent.TryEnterVehicleAsDriver(car, _agent));
        Assert.Equal(Whereabouts.Vehicle, _agent.Whereabouts);

        Assert.True(_agent.TryLeaveVehicle(_agent));
        Assert.Equal(Whereabouts.Offside, _agent.Whereabouts);
    }

    [Fact]
    public void WalkEmptyRoute()
    {
        var route = _layer.Search(_agent, _start, _start, ModalChoice.Walking);
        _agent.MultimodalRoute = route;
        Assert.True(_agent.GoalReached);

        for (var tick = 0;
             tick < 10 && !_agent.GoalReached;
             tick++, _layer.Context.UpdateStep()) _agent.Tick();

        Assert.True(_agent.GoalReached);
    }

    [Fact]
    public void WalkToGoal()
    {
        var route = _layer.Search(_agent, _start, _goal, ModalChoice.Walking);
        _agent.MultimodalRoute = route;

        for (var tick = 0; tick < 10000 && !_agent.GoalReached; tick++, _layer.Context.UpdateStep())
        {
            _agent.Tick();
            Assert.Equal(ModalChoice.Walking, _agent.ActiveCapability);
        }

        Assert.True(_agent.GoalReached);
        Assert.Equal(_agent.Position, route.Goal);
    }
}