using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Bicycle.Rental;
using SOHModel.Domain.Graph;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalAgentTests;

public class MultimodalResolverProvideTests
{
    private readonly SpatialGraphEnvironment _environment;
    private readonly TestMultimodalLayer _multimodalLayer;

    public MultimodalResolverProvideTests()
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
        var bicycleParkingLayer = new BicycleParkingLayerFixture(_environment).BicycleParkingLayer;

        _multimodalLayer = new TestMultimodalLayer(_environment)
        {
            CarParkingLayer = carParkingLayer,
            BicycleRentalLayer = bicycleRentalLayer,
            BicycleParkingLayer = bicycleParkingLayer
        };
    }

    [Fact]
    public void TestProvidesForWalking()
    {
        var agent = new TestCapabilitiesAgent(ModalChoice.Walking);
        foreach (var node in _environment.Nodes)
            Assert.Contains(ModalChoice.Walking, _multimodalLayer.Provides(agent, node));
    }

    [Fact]
    public void TestProvidesForRentalCycling()
    {
        var agent = new TestCapabilitiesAgent(ModalChoice.CyclingRentalBike);
        var foundNodes = new HashSet<ISpatialNode>();

        foreach (var rentalStation in _multimodalLayer.BicycleRentalLayer.Features.OfType<BicycleRentalStation>())
        {
            var nearestNode = _environment.NearestNode(rentalStation.Position);
            if (!_multimodalLayer.Provides(agent, nearestNode).Contains(ModalChoice.CyclingRentalBike))
                _multimodalLayer.Provides(agent, nearestNode);

            Assert.Contains(ModalChoice.CyclingRentalBike, _multimodalLayer.Provides(agent, nearestNode));
            foundNodes.Add(nearestNode);
        }

        foreach (var node in _environment.Nodes)
            if (!foundNodes.Contains(node))
                Assert.DoesNotContain(ModalChoice.CyclingRentalBike, _multimodalLayer.Provides(agent, node));
    }

    [Fact]
    public void TestProvidesForCyclingOwnBike()
    {
        var start = Position.CreateGeoPosition(9.9546178, 53.557155);
        var bicycle = _multimodalLayer.BicycleParkingLayer.CreateOwnBicycleNear(start, -1, 0f);
        var agent = new TestCapabilitiesAgent(ModalChoice.CyclingOwnBike)
        {
            Bicycle = bicycle
        };

        var bicycleNode = _environment.NearestNode(bicycle.Position);
        Assert.Contains(ModalChoice.CyclingOwnBike, _multimodalLayer.Provides(agent, bicycleNode));

        foreach (var node in _environment.Nodes)
            if (!bicycleNode.Equals(node))
                Assert.DoesNotContain(ModalChoice.CyclingOwnBike, _multimodalLayer.Provides(agent, node));
    }

    [Fact]
    public void TestProvidesForDriving()
    {
        var start = Position.CreateGeoPosition(9.9546178, 53.557155);
        var car = _multimodalLayer.CarParkingLayer.CreateOwnCarNear(start);
        var agent = new TestCapabilitiesAgent(ModalChoice.CarDriving)
        {
            Car = car
        };

        var carNode = _environment.NearestNode(start);
        Assert.Contains(ModalChoice.CarDriving, _multimodalLayer.Provides(agent, carNode));

        foreach (var node in _environment.Nodes)
            if (!carNode.Equals(node))
                Assert.DoesNotContain(ModalChoice.CarDriving, _multimodalLayer.Provides(agent, node));
    }
}