using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHBicycleModel.Parking;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.BicycleModelTests;

public class BicycleParkingLayerTests
{
    [Fact]
    public void TestCreateOwnBicycleOnNodes()
    {
        var bicycleParkingLayer = CreateBicycleParkingLayer();

        var centre = bicycleParkingLayer.Features.First().VectorStructured.Geometry.Coordinate;
        var position = Position.CreateGeoPosition(centre.X, centre.Y);

        var bicycle = bicycleParkingLayer.CreateOwnBicycleNear(position, 20, 0);
        Assert.NotNull(bicycle);
        Assert.Null(bicycle.BicycleParkingLot);
        Assert.InRange(bicycle.Position.DistanceInMTo(position), 0, 20);
        Assert.NotNull(bicycle.Environment);
        Assert.Contains(bicycle, bicycle.Environment.Entities.Keys);
    }

    [Fact]
    public void TestCreateOwnBicycleInParkingLotWithRadius()
    {
        var bicycleParkingLayer = CreateBicycleParkingLayer();

        var centre = bicycleParkingLayer.Features.First().VectorStructured.Geometry.Coordinate;
        var position = Position.CreateGeoPosition(centre.X, centre.Y);

        var bicycle = bicycleParkingLayer.CreateOwnBicycleNear(position, 20, 1);
        Assert.NotNull(bicycle);
        Assert.NotNull(bicycle.BicycleParkingLot);
        Assert.InRange(bicycle.Position.DistanceInMTo(position), 0, 20);
        Assert.NotNull(bicycle.Environment);
        Assert.Contains(bicycle, bicycle.Environment.Entities.Keys);

        var bicycle2 = bicycleParkingLayer.CreateOwnBicycleNear(position, 50, 1);
        Assert.NotNull(bicycle2);
        Assert.NotNull(bicycle2.BicycleParkingLot);
        Assert.InRange(bicycle2.Position.DistanceInMTo(position), 0, 50);
        Assert.NotNull(bicycle2.Environment);
        Assert.Contains(bicycle2, bicycle2.Environment.Entities.Keys);
    }

    private static BicycleParkingLayer CreateBicycleParkingLayer()
    {
        var environment = new SpatialGraphEnvironment(new Input
        {
            File = ResourcesConstants.DriveGraphAltonaAltstadt,
            InputConfiguration = new InputConfiguration
            {
                IsBiDirectedImport = true,
                Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Cycling }
            }
        });

        var bicycleParkingLayer = new BicycleParkingLayerFixture(environment).BicycleParkingLayer;
        return bicycleParkingLayer;
    }
}