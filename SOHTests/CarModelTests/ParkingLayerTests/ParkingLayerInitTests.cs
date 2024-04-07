using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Data;
using NetTopologySuite.Geometries;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;
using SOHTests.Commons.Layer;
using Xunit;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHTests.CarModelTests.ParkingLayerTests;

public class ParkingLayerInitTests
{
    private readonly CarParkingLayer _carParkingLayer;

    public ParkingLayerInitTests()
    {
        var streetLayer = new StreetLayer();
        _carParkingLayer = new CarParkingLayerFixture(streetLayer).CarParkingLayer;
    }

    [Fact]
    public void InitializeParkingSpacesRandomly()
    {
        var position = Position.CreateGeoPosition(9.931294, 53.554248);
        var parkingSpace = _carParkingLayer.Nearest(position);
        Assert.NotNull(parkingSpace);
        Assert.True(parkingSpace.HasCapacity);
        Assert.Equal(1, parkingSpace.Capacity);
    }

    [Fact]
    public void InsertCarInParkingSpace()
    {
        var parkingSpace = new CarParkingSpace();
        parkingSpace.Init(null, new VectorStructuredData
        {
            Data = new Dictionary<string, object>(),
            Geometry = new Point(9.812028, 53.560606)
        });
        Assert.NotNull(parkingSpace);
        Assert.True(parkingSpace.HasCapacity);
        Assert.Equal(1, parkingSpace.Capacity);

        var car = Golf.Create(_carParkingLayer);
        Assert.True(parkingSpace.Enter(car));
        Assert.False(parkingSpace.HasCapacity);
        Assert.Single(parkingSpace.ParkingVehicles);
        Assert.Equal(car, parkingSpace.ParkingVehicles.First().Key);

        Assert.Throws<ArgumentException>(() => parkingSpace.Enter(car));

        var lateCar = Golf.Create(_carParkingLayer);
        Assert.False(parkingSpace.Enter(lateCar));

        parkingSpace.Leave(car);
        Assert.True(parkingSpace.HasCapacity);
    }
}