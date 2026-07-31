using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Bicycle.Rental;
using SOHModel.Car.Model;
using SOHModel.Domain.Graph;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalAgentTests;

public class MultimodalResolverConsumeTests
{
    private readonly SpatialGraphEnvironment _environment;
    private readonly TestMultimodalLayer _multimodalLayer;

    public MultimodalResolverConsumeTests()
    {
        var options = new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Cycling }
                    }
                },
                new()
                {
                    File = ResourcesConstants.WalkGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving }
                    }
                }
            }
        };

        _environment = new SpatialGraphEnvironment(options);

        var bicycleRentalLayer = new BicycleRentalLayerFixture(_environment).BicycleRentalLayer;
        var carParkingLayer = new CarParkingLayerFixture(new StreetLayer { Environment = _environment })
            .CarParkingLayer;

        _multimodalLayer = new TestMultimodalLayer(_environment)
        {
            CarParkingLayer = carParkingLayer,
            BicycleRentalLayer = bicycleRentalLayer
        };
    }

    [Fact]
    public void TestConsumeForWalking()
    {
        foreach (var node in _environment.Nodes)
            Assert.True(_multimodalLayer.Consumes(ModalChoice.Walking, node));
    }

    [Fact]
    public void TestConsumesForRentalCycling()
    {
        var foundNodes = new HashSet<ISpatialNode>();
        foreach (var rentalStation in _multimodalLayer.BicycleRentalLayer.Features.OfType<BicycleRentalStation>())
        {
            var nearestNode = _environment.NearestNode(rentalStation.Position);

            Assert.True(_multimodalLayer.Consumes(ModalChoice.CyclingRentalBike, nearestNode));
            foundNodes.Add(nearestNode);
        }

        foreach (var node in _environment.Nodes)
            if (!foundNodes.Contains(node))
                Assert.False(_multimodalLayer.Consumes(ModalChoice.CyclingRentalBike, node));
    }

    [Fact]
    public void TestConsumesForCyclingOwnBike()
    {
        foreach (var node in _environment.Nodes)
            Assert.True(_multimodalLayer.Consumes(ModalChoice.CyclingOwnBike, node));
    }

    [Fact]
    public void TestConsumesForDriving()
    {
        var carParkingSpace = _multimodalLayer.CarParkingLayer.Nearest(
                Position.CreateGeoPosition(9.9528571, 53.5505072));
        Assert.NotNull(carParkingSpace);
        var nearestNode = _environment.NearestNode(carParkingSpace.Position);

        Assert.True(carParkingSpace.HasCapacity);
        Assert.True(_multimodalLayer.Consumes(ModalChoice.CarDriving, nearestNode));

        // Occupy via public API (not Occupied=true) so parking bookkeeping stays consistent.
        var parkedCars = new List<Car>();
        for (var i = 0; i < carParkingSpace.Capacity; i++)
        {
            var car = new Car();
            Assert.True(carParkingSpace.Enter(car));
            parkedCars.Add(car);
        }

        Assert.False(carParkingSpace.HasCapacity);
        Assert.False(carParkingSpace.Enter(new Car()));

        foreach (var car in parkedCars)
            Assert.True(carParkingSpace.Leave(car));

        Assert.True(carParkingSpace.HasCapacity);
        Assert.True(_multimodalLayer.Consumes(ModalChoice.CarDriving, nearestNode));
    }
}