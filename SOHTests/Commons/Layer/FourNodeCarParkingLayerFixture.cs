using System;
using System.Collections.Generic;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using Moq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHDomain.Graph;
using SOHTests.Commons.Environment;

namespace SOHTests.Commons.Layer;

public class FourNodeCarParkingLayerFixture : IDisposable
{
    public FourNodeCarParkingLayerFixture(ISpatialGraphLayer streetLayer)
    {
        var features = new List<IFeature>
        {
            new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 0 } },
                Geometry = new Point(FourNodeGraphEnv.Node2Pos.X, FourNodeGraphEnv.Node2Pos.Y)
            },
            new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 0 } },
                Geometry = new Point(FourNodeGraphEnv.Node2Pos.X, FourNodeGraphEnv.Node2Pos.Y)
            },
            new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 100 } },
                Geometry = new Point(FourNodeGraphEnv.Node3Pos.X, FourNodeGraphEnv.Node3Pos.Y)
            }
        };
        var dataTable = new CsvReader(ResourcesConstants.CarCsv, true).ToTable();
        var entityManagerImpl = new EntityManagerImpl((typeof(Car), dataTable));
        var mock = new Mock<ISimulationContainer>();

        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(entityManagerImpl);
        var mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features
            },
            Container = mock.Object
        };

        CarParkingLayer = new CarParkingLayer { StreetLayer = streetLayer };
        CarParkingLayer.InitLayer(mapping);
    }

    public CarParkingLayer CarParkingLayer { get; }

    public void Dispose()
    {
        CarParkingLayer.Dispose();
    }
}