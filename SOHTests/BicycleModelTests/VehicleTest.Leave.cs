using System;
using Xunit;

namespace SOHTests.BicycleModelTests;

public partial class VehicleTest
{
    [Fact]
    public void LeaveVehicleAsDriver()
    {
        var vehicle = new TestBicycle(new Random().Next());

        var driver = new TestBicycleDriver();
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
        var vehicle = new TestBicycle(1);

        var driver = new TestBicycleDriver();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var passenger = new TestBicycleDriver();
        passenger.Bicycle = vehicle;
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
        var vehicle = new TestBicycle(new Random().Next());

        var driver = new TestBicycleDriver();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = driverVehicle.LeaveVehicle(new TestBicycleDriver());
        Assert.False(result2);
    }

    [Fact]
    public void LeaveVehicleAsPassenger()
    {
        var vehicle = new TestBicycle(1);

        var passenger = new TestBicycleDriver();
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
        var vehicle = new TestBicycle(10);

        var passenger = new TestBicycleDriver();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result2 = passengerVehicle.LeaveVehicle(new TestBicycleDriver());
        Assert.False(result2);
    }
}