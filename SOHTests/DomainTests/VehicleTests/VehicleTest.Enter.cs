using System;
using Xunit;

namespace SOHTests.DomainTests.VehicleTests;

public partial class VehicleTest
{
    [Fact]
    public void EnterDriverAsPassenger()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = vehicle.TryEnterPassenger(driver, out var driverVehicle2);
        Assert.False(result2);
        Assert.Null(driverVehicle2);
        Assert.DoesNotContain(driver, vehicle.Passengers);
    }

    [Fact]
    public void EnterDriverInEmptyVehicle()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);
    }

    [Fact]
    public void EnterDriverInOccupiedVehicle()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var firstDriver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(firstDriver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(firstDriver, vehicle.Driver);

        var secondDriver = new TestSteeringCapable();
        var result2 = vehicle.TryEnterDriver(secondDriver, out var driverVehicle2);
        Assert.False(result2);
        Assert.Null(driverVehicle2);
        Assert.NotEqual(secondDriver, vehicle.Driver);
    }

    [Fact]
    public void EnterDriverTwice()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, new Random().Next());

        var driver = new TestSteeringCapable();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);

        var result2 = vehicle.TryEnterDriver(driver, out var driverVehicle2);
        Assert.False(result2);
        Assert.Null(driverVehicle2);
        Assert.Equal(driver, vehicle.Driver);
    }

    [Fact]
    public void EnterPassengerAsDriver()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 1);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result2 = vehicle.TryEnterDriver(passenger, out var driverVehicle);
        Assert.False(result2);
        Assert.Null(driverVehicle);
        Assert.NotEqual(passenger, vehicle.Driver);
    }

    [Fact]
    public void EnterPassengerInVehicleWithFreeSeats()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 2);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var passenger2 = new TestSteeringCapable();
        var result2 = vehicle.TryEnterPassenger(passenger2, out var passengerVehicle2);
        Assert.True(result2);
        Assert.NotNull(passengerVehicle2);
        Assert.Contains(passenger2, vehicle.Passengers);
    }

    [Fact]
    public void EnterPassengerInVehicleWithoutCapacity()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 0);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.False(result);
        Assert.Null(passengerVehicle);
        Assert.DoesNotContain(passenger, vehicle.Passengers);
    }

    [Fact]
    public void EnterPassengerInVehicleWithoutFreeSeats()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 1);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var passenger2 = new TestSteeringCapable();
        var result2 = vehicle.TryEnterPassenger(passenger2, out var passengerVehicle2);
        Assert.False(result2);
        Assert.Null(passengerVehicle2);
        Assert.DoesNotContain(passenger2, vehicle.Passengers);
    }

    [Fact]
    public void EnterPassengerTwice()
    {
        var vehicle = new TestCarWithoutRangeCheck(_environment, 1);

        var passenger = new TestSteeringCapable();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var result2 = vehicle.TryEnterPassenger(passenger, out var passengerVehicle2);
        Assert.False(result2);
        Assert.Null(passengerVehicle2);
        Assert.Contains(passenger, vehicle.Passengers);
    }
}