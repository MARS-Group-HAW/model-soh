using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using Moq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;
using Xunit;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHTests.CarModelTests.ParkingLayerTests;

public class ParkingSpaceOccupancyTests
{
    private const int CountSpaces = 1000;
    private static readonly LayerInitData Mapping;
    private readonly ICarParkingLayer _parkingLayer = new CarParkingLayer { StreetLayer = new StreetLayer() };

    static ParkingSpaceOccupancyTests()
    {
        var features = new List<IFeature>();
        for (var i = 0; i < CountSpaces; i++)
            features.Add(new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 0 } },
                Geometry = new Point(10.00553, 53.56310)
            });
        var dataTable = CsvReader.MapData(ResourcesConstants.CarCsv);
        var manager = new EntityManagerImpl(dataTable);

        var mock = new Mock<ISimulationContainer>();
        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(manager);
        Mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features
            },
            Container = mock.Object
        };
    }

    public ParkingSpaceOccupancyTests()
    {
        _parkingLayer.InitLayer(Mapping);
    }

    [Fact]
    public void TestOccupyAll()
    {
        var spaces = _parkingLayer.Features.OfType<CarParkingSpace>().ToList();
        Assert.Equal(CountSpaces, spaces.Count);

        Assert.All(spaces, s => Assert.True(s.HasCapacity));

        _parkingLayer.UpdateOccupancy(1.0);
        Assert.All(spaces, s => Assert.False(s.HasCapacity));

        _parkingLayer.UpdateOccupancy(0);
        Assert.All(spaces, s => Assert.True(s.HasCapacity));
    }

    [Fact]
    public void TestOccupyByAgentCount()
    {
        var spaces = _parkingLayer.Features.OfType<CarParkingSpace>().ToList();
        var count = spaces.Count;

        //for as many cars as available parking spaces
        const int carCount = (int)(CountSpaces / 2.0);
        _parkingLayer.UpdateOccupancy(0, carCount);
        Assert.All(spaces, s => Assert.True(s.HasCapacity));
        Assert.Equal(CountSpaces, count);

        //for half the cars to available parking spaces
        _parkingLayer.UpdateOccupancy(1.0, carCount);
        var freeSpaces = spaces.Where(s => s.HasCapacity);
        Assert.InRange(freeSpaces.Count(), count * 0.45, count * 0.55);

        //without cars
        _parkingLayer.UpdateOccupancy(1.0);
        freeSpaces = spaces.Where(s => s.HasCapacity);
        Assert.InRange(freeSpaces.Count(), 0, CountSpaces * 0.1);

        //full occupancy but used with cars, so effectively no occupancy at all
        _parkingLayer.UpdateOccupancy(1.0, CountSpaces);
        freeSpaces = spaces.Where(s => s.HasCapacity);
        Assert.InRange(freeSpaces.Count(), CountSpaces * 0.99, CountSpaces);
    }

    [Fact]
    public void TestOccupyHalf()
    {
        var spaces = _parkingLayer.Features.OfType<CarParkingSpace>().ToList();
        var overallCount = spaces.Count;
        Assert.Equal(CountSpaces, overallCount);
        Assert.All(spaces, s => Assert.True(s.HasCapacity));

        _parkingLayer.UpdateOccupancy(0.5);
        var freeSpaces = spaces.Where(s => s.HasCapacity);
        Assert.InRange(freeSpaces.Count(), overallCount * 0.40, overallCount * 0.60);
    }

    [Fact]
    public void TestDoNotFindOccupiedParkingSpace()
    {
        var position = Position.CreateGeoPosition(9.931294, 53.554248);
        var space = _parkingLayer.Nearest(position);
        Assert.NotNull(space);
        Assert.True(space.HasCapacity);
        space.Occupied = true;

        var parkingSpace = _parkingLayer.Nearest(position);
        Assert.NotEqual(space, parkingSpace);
    }

    [Fact]
    public void TestDoNotFindOccupancyProbability()
    {
        var parkingLayer = new CarParkingLayer { StreetLayer = new StreetLayer(), OccupancyProbability = 100 };
        parkingLayer.InitLayer(Mapping);

        var space = parkingLayer.Nearest(Position.CreatePosition(9.931294, 53.554248));
        Assert.Null(space);
    }
}