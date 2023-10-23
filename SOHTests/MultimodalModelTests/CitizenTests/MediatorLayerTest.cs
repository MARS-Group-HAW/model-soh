using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHDomain.Common;
using SOHMultimodalModel.Layers;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.CitizenTests;

public class MediatorLayerTest : IClassFixture<MediatorLayerAltonaAltstadtFixture>
{
    private readonly MediatorLayer _mediatorLayer;

    public MediatorLayerTest(MediatorLayerAltonaAltstadtFixture mediatorFixture)
    {
        _mediatorLayer = mediatorFixture.MediatorLayer;
    }

    [Fact]
    public void CheckGetNextPoiOfTypeOutOfRadius()
    {
        //stand 60 meters away from poi
        var sourcePosition = Position.CreateGeoPosition(9.946563, 53.544225);
        var position = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.Hotel, true, 5);

        Assert.Null(position);

        position = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.Hotel, true, 30);

        Assert.Equal(9.9465472, position.X, 7);
        Assert.Equal(53.5444828, position.Y, 7);
    }

    [Fact]
    public void CheckGetNextPoiOfTypeWithoutRadius()
    {
        //stand in front of POI
        var sourcePosition = Position.CreateGeoPosition(9.94512408, 53.55456001);
        var targetPosition = Position.CreateGeoPosition(9.94557110, 53.55525420);

        var foundPosition = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.Kindergarten);
        Assert.Equal(targetPosition.Latitude, foundPosition.Latitude, 7);
        Assert.Equal(targetPosition.Longitude, foundPosition.Longitude, 7);

        //stand in front of land use one of these
        sourcePosition = Position.CreateGeoPosition(9.93441760, 53.54642858);
        var positions = new List<Position>
        {
            Position.CreateGeoPosition(9.932083, 53.5472762),
            Position.CreateGeoPosition(9.9333151, 53.547287),
            Position.CreateGeoPosition(9.9337917, 53.5473889),
            Position.CreateGeoPosition(9.9340689, 53.547482399999986),
            Position.CreateGeoPosition(9.934115, 53.54764620000001),
            Position.CreateGeoPosition(9.9343297, 53.5476148),
            Position.CreateGeoPosition(9.9345752, 53.547594),
            Position.CreateGeoPosition(9.9345643, 53.5465975),
            Position.CreateGeoPosition(9.9345622, 53.5465544),
            Position.CreateGeoPosition(9.9344109, 53.5464864),
            Position.CreateGeoPosition(9.9337137, 53.5465503),
            Position.CreateGeoPosition(9.9331789, 53.5466062),
            Position.CreateGeoPosition(9.9330615, 53.5466508),
            Position.CreateGeoPosition(9.9329168, 53.5466641),
            Position.CreateGeoPosition(9.9327359, 53.5466526),
            Position.CreateGeoPosition(9.9323701, 53.5466946),
            Position.CreateGeoPosition(9.9321799, 53.5467631),
            Position.CreateGeoPosition(9.9321321, 53.5470388),
            Position.CreateGeoPosition(9.932083, 53.5472762)
        };
        foundPosition = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.ParkLandUse);

        Assert.Contains(foundPosition, positions);

        //stand in front of building
        sourcePosition = Position.CreateGeoPosition(9.94139233, 53.54798699);

        positions = new List<Position>
        {
            Position.CreateGeoPosition(9.941168, 53.5481468),
            Position.CreateGeoPosition(9.9415468, 53.5481889),
            Position.CreateGeoPosition(9.941577, 53.5480912),
            Position.CreateGeoPosition(9.9411909, 53.5480562)
        };

        foundPosition = _mediatorLayer.GetNextPoiOfType(sourcePosition, 1500);
        Assert.Contains(foundPosition, positions);
    }

    [Fact]
    public void CheckGetNextPoiOfTypeWithRadius()
    {
        //stand 60 meters away from poi
        var sourcePosition = Position.CreateGeoPosition(9.94556643, 53.54431606);
        var targetPosition = Position.CreateGeoPosition(9.9455592, 53.5444917);

        var foundPosition = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.Hotel, true, 80);
        Assert.Equal(targetPosition.Latitude, foundPosition.Latitude, 7);
        Assert.Equal(targetPosition.Longitude, foundPosition.Longitude, 7);

        var positions = new List<Position>
        {
            Position.CreateGeoPosition(9.932083, 53.5472762),
            Position.CreateGeoPosition(9.9333151, 53.547287),
            Position.CreateGeoPosition(9.9337917, 53.5473889),
            Position.CreateGeoPosition(9.9340689, 53.547482399999986),
            Position.CreateGeoPosition(9.934115, 53.54764620000001),
            Position.CreateGeoPosition(9.9343297, 53.5476148),
            Position.CreateGeoPosition(9.9345752, 53.547594),
            Position.CreateGeoPosition(9.9345643, 53.5465975),
            Position.CreateGeoPosition(9.9345622, 53.5465544),
            Position.CreateGeoPosition(9.9344109, 53.5464864),
            Position.CreateGeoPosition(9.9337137, 53.5465503),
            Position.CreateGeoPosition(9.9331789, 53.5466062),
            Position.CreateGeoPosition(9.9330615, 53.5466508),
            Position.CreateGeoPosition(9.9329168, 53.5466641),
            Position.CreateGeoPosition(9.9327359, 53.5466526),
            Position.CreateGeoPosition(9.9323701, 53.5466946),
            Position.CreateGeoPosition(9.9321799, 53.5467631),
            Position.CreateGeoPosition(9.9321321, 53.5470388),
            Position.CreateGeoPosition(9.932083, 53.5472762)
        };
        sourcePosition = Position.CreateGeoPosition(9.93441760, 53.54642858);
        foundPosition = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.ParkLandUse, true, 300);

        Assert.Contains(foundPosition, positions);
    }

    [Fact]
    public void CheckGetNextPoIofTypeWithTypeOutOfRange()
    {
        var sourcePosition = Position.CreateGeoPosition(9.9389376, 53.5549218);
        var targetPosition = Position.CreateGeoPosition(9.9389139, 53.5549624);

        var position = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.Playground, true, 10);
        Assert.Equal(targetPosition.Longitude, position.Longitude, 7);
        Assert.Equal(targetPosition.Latitude, position.Latitude, 7);

        position = _mediatorLayer.GetNextPoiOfType(sourcePosition, OsmFeatureCodes.ParkPoi, true, 10);
        Assert.Null(position);
    }

    [Fact]
    public void CheckGetNextPoIofTypeWithWrongType()
    {
        var sourcePosition = Position.CreateGeoPosition(9.9856634, 53.5491964);
        var position = _mediatorLayer.GetNextPoiOfType(sourcePosition, 0, true, 1);
        Assert.Null(position);
    }

    [Fact]
    public void CheckGetPoIofTypeInRange()
    {
        var sourcePosition = Position.CreateGeoPosition(9.94173415, 53.55491127);
        var targetPosition = Position.CreateGeoPosition(9.9418009, 53.5548917);
        var position = _mediatorLayer.GetPoIofTypeInRange(sourcePosition, OsmFeatureCodes.Leisure,
            OsmFeatureCodes.DogPark, true, 10);
        Assert.Equal(targetPosition.Longitude, position.Longitude, 7);
        Assert.Equal(targetPosition.Latitude, position.Latitude, 7);
    }

    [Fact]
    public void CheckGetPoIofTypeInRangeWithWrongRange()
    {
        var sourcePosition = Position.CreateGeoPosition(9.946934, 53.561126);

        Assert.Throws<ArgumentException>(() =>
            _mediatorLayer.GetPoIofTypeInRange(sourcePosition, 0, 1));
    }

    [Fact]
    public void GetPoiWithOneOutOfManyTypes()
    {
        var sourcePosition = Position.CreateGeoPosition(9.9389376, 53.5549218);
        var targetPosition = Position.CreateGeoPosition(9.9389139, 53.5549624);

        var listOfTypes = new List<int>
            { OsmFeatureCodes.ParkPoi, OsmFeatureCodes.Playground, OsmFeatureCodes.Forest };

        var position = _mediatorLayer.FindNextNearestLocationForAnyTarget(sourcePosition, listOfTypes, true, 10);

        Assert.Equal(targetPosition.Latitude, position.Latitude, 7);
        Assert.Equal(targetPosition.Longitude, position.Longitude, 7);
    }

    [Fact]
    public void GetPoiWithOneOutOfManyTypesWithEmptyList()
    {
        var sourcePosition = Position.CreateGeoPosition(9.946934, 53.561126);

        Assert.Throws<ArgumentException>(() =>
            _mediatorLayer.FindNextNearestLocationForAnyTarget(sourcePosition, new List<int>()));
    }

    [Fact]
    public void GetPoiWithOneOutOfManyTypesWithNoPoIinMaxRadius()
    {
        var sourcePosition = Position.CreateGeoPosition(9.9463861, 53.5443706);
        var listOfTypes = new List<int> { OsmFeatureCodes.Restaurant, OsmFeatureCodes.FastFood };
        var position = _mediatorLayer.FindNextNearestLocationForAnyTarget(sourcePosition, listOfTypes);

        Assert.Equal(20.15, sourcePosition.DistanceInMTo(position), 2);
    }
}