using System;
using System.Collections.Generic;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SOHBicycleModel.Rental;
using SOHDomain.Graph;
using SOHTests.Commons.Environment;

namespace SOHTests.Commons.Layer;

public class FourNodeBicycleRentalLayerFixture : IDisposable
{
    public readonly BicycleRentalLayer BicycleRentalLayer;

    public FourNodeBicycleRentalLayerFixture(ISpatialGraphLayer spatialGraphLayer)
    {
        var features = new List<IFeature>
        {
            new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 0 }, { "name", "BikeRentalNode2" } },
                Geometry = new Point(FourNodeGraphEnv.Node2Pos.X, FourNodeGraphEnv.Node2Pos.Y)
            },
            new VectorStructuredData
            {
                Data = new Dictionary<string, object> { { "area", 0 }, { "name", "BikeRentalNode3" } },
                Geometry = new Point(FourNodeGraphEnv.Node3Pos.X, FourNodeGraphEnv.Node3Pos.Y)
            }
        };

        var mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features
            }
        };

        BicycleRentalLayer = new BicycleRentalLayer
        {
            SpatialGraphMediatorLayer = new SpatialGraphMediatorLayer
            {
                Environment = spatialGraphLayer.Environment
            }
        };
        BicycleRentalLayer.InitLayer(mapping);
    }

    public void Dispose()
    {
        BicycleRentalLayer.Dispose();
    }
}