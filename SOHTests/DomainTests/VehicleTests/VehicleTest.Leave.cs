using System;
using Xunit;

namespace SOHTests.DomainTests.VehicleTests;

public partial class VehicleTest
{
    [Fact]
    public void LeaveBicycleAsDriver()
    {
        var vehicle = new TestBicycleWithoutRangeCheck();

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = driverVehicle.LeaveVehicle(driver);
        Assert.True(result2);
    }

    [Fact]
    public void LeaveCarAsDriver()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = driverVehicle.LeaveVehicle(driver);
        Assert.True(result2);
    }

    [Fact]
    public void LeaveVehicleAsDriverPassengersAreInformed()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 1);

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var passenger = new TestSteeringCapable();
        passenger.CarWithoutRangeCheck = vehicle;
        var result2 = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result2);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result3 = driverVehicle.LeaveVehicle(driver);
        Assert.True(result3);
    }

    [Fact]
    public void LeaveVehicleAsDriverWhichIsNotTheDriver()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = driverVehicle.LeaveVehicle(new TestSteeringCapable());
        Assert.False(result2);
    }

    [Fact]
    public void LeaveVehicleAsPassenger()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 1);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result2 = passengerVehicle.LeaveVehicle(passenger);
        Assert.True(result2);
    }


    [Fact]
    public void LeaveVehicleAsPassengerThatIsNoPassenger()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 10);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result2 = passengerVehicle.LeaveVehicle(new TestSteeringCapable());
        Assert.False(result2);
    }
}