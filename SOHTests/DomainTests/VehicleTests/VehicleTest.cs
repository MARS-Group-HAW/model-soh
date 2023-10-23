using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHBicycleModel.Steering;
using SOHCarModel.Model;
using SOHCarModel.Steering;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Common;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.DomainTests.VehicleTests;

public partial class VehicleTest
{
    private readonly ISpatialGraphEnvironment _environment = new SpatialGraphEnvironment();

    [Fact]
    public void VehicleHasPositionAfterInsertIntoEnvironment()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var environment = fourNodeGraphEnv.GraphEnvironment;

        var car = Golf.Create(environment);
        Assert.Null(car.Position);

        Assert.True(environment.Insert(car, fourNodeGraphEnv.Node1));
        Assert.Equal(fourNodeGraphEnv.Node1.Position, car.Position);
    }
}

internal class TestSteeringCapable : ICarSteeringCapable, IBicycleSteeringCapable
{
    public TestCarWithoutRangeCheck CarWithoutRangeCheck;

    public TestSteeringCapable()
    {
        Position = Position.CreatePosition(0, 0);
    }

    public double DriverRandom => 0d;
    public DriverType DriverType => DriverType.Normal;
    public double CyclingPower => 0d;
    public double Mass => 80;
    public double Gradient => 0;
    public Bicycle Bicycle => null;

    public Position Position { get; set; }

    public void Notify(PassengerMessage passengerMessage)
    {
        if (CarWithoutRangeCheck != null && passengerMessage.Equals(PassengerMessage.NoDriver))
            Assert.Null(CarWithoutRangeCheck.Driver);
    }

    public Car Car => CarWithoutRangeCheck;
    public bool OvertakingActivated => false;

    public bool BrakingActivated
    {
        get => false;
        set { }
    }

    public bool CurrentlyCarDriving => Car?.Driver.Equals(this) ?? false;
}

internal sealed class TestCarWithoutRangeCheck : Car
{
    public TestCarWithoutRangeCheck(ISpatialGraphEnvironment environment, int passengerCapacity = 4)
    {
        Environment = environment;
        PassengerCapacity = passengerCapacity;
        Position = Position.CreatePosition(0, 0);
    }

    protected override bool IsInRangeToEnterVehicle(IPassengerCapable passenger)
    {
        return true;
    }
}

internal class TestBicycleWithoutRangeCheck : Bicycle
{
    protected override bool IsInRangeToEnterVehicle(IPassengerCapable passenger)
    {
        return true;
    }
}