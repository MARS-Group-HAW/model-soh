using System;
using Xunit;

namespace SOHTests.BicycleModelTests;

public partial class VehicleTest
{
    [Fact]
    public void EnterDriverAsPassenger()
    {
        var vehicle = new TestBicycle(new Random().Next());

        var driver = new TestBicycleDriver();
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
        var vehicle = new TestBicycle(new Random().Next());

        var driver = new TestBicycleDriver();
        var result = vehicle.TryEnterDriver(driver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(driver, vehicle.Driver);
    }

    [Fact]
    public void EnterDriverInOccupiedVehicle()
    {
        var vehicle = new TestBicycle(new Random().Next());

        var firstDriver = new TestBicycleDriver();
        var result = vehicle.TryEnterDriver(firstDriver, out var driverVehicle);
        Assert.True(result);
        Assert.NotNull(driverVehicle);
        Assert.Equal(firstDriver, vehicle.Driver);

        var secondDriver = new TestBicycleDriver();
        var result2 = vehicle.TryEnterDriver(secondDriver, out var driverVehicle2);
        Assert.False(result2);
        Assert.Null(driverVehicle2);
        Assert.NotEqual(secondDriver, vehicle.Driver);
    }

    [Fact]
    public void EnterDriverTwice()
    {
        var vehicle = new TestBicycle(new Random().Next());

        var driver = new TestBicycleDriver();
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
        var vehicle = new TestBicycle(1);

        var passenger = new TestBicycleDriver();
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
        var vehicle = new TestBicycle(2);

        var passenger = new TestBicycleDriver();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);
    }

    // TODO should capacity for bicycles should be a parameter?
//        [Fact]
//        public void EnterPassengerInVehicleWithoutCapacity()
//        {
//            var vehicle = new TestVehicle(0);
//
//            var passenger = new TestVehicleDriver();
//            var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
//            Assert.False(result);
//            Assert.Null(passengerVehicle);
//            Assert.DoesNotContain(passenger, vehicle.Passengers);
//        }

    [Fact]
    public void EnterPassengerInVehicleWithoutFreeSeats()
    {
        var vehicle = new TestBicycle(1);

        var passenger = new TestBicycleDriver();
        var result = vehicle.TryEnterPassenger(passenger, out var passengerVehicle);
        Assert.True(result);
        Assert.NotNull(passengerVehicle);
        Assert.Contains(passenger, vehicle.Passengers);

        var passenger2 = new TestBicycleDriver();
        var result2 = vehicle.TryEnterPassenger(passenger2, out var passengerVehicle2);
        Assert.False(result2);
        Assert.Null(passengerVehicle2);
        Assert.DoesNotContain(passenger2, vehicle.Passengers);
    }

    [Fact]
    public void EnterPassengerTwice()
    {
        var vehicle = new TestBicycle(1);

        var passenger = new TestBicycleDriver();
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