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
using SOHCarModel.Rental;
using SOHDomain.Graph;
using SOHTests.Commons.Environment;

namespace SOHTests.Commons.Layer;

public class FourNodeCarRentalLayerFixture
{
    public FourNodeCarRentalLayerFixture()
    {
        FourNodeGraphEnv = new FourNodeGraphEnv();

        var features = new List<IFeature>
        {
            new VectorStructuredData
            {
                Data = new Dictionary<string, object>(),
                Geometry = new Point(FourNodeGraphEnv.Node2Pos.X, FourNodeGraphEnv.Node2Pos.Y)
            },
            new VectorStructuredData
            {
                Data = new Dictionary<string, object>(),
                Geometry = new Point(FourNodeGraphEnv.Node3Pos.X, FourNodeGraphEnv.Node3Pos.Y)
            }
        };
        var dataTable = new CsvReader(ResourcesConstants.CarCsv, true).ToTable();
        var entityManagerImpl = new EntityManagerImpl((typeof(RentalCar), dataTable));
        var mock = new Mock<ISimulationContainer>();

        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(entityManagerImpl);
        var mapping = new LayerInitData
        {
            LayerInitConfig = new LayerMapping
            {
                Value = features,
                IndividualMapping = new List<IndividualMapping>
                {
                    new() { Name = "carKeyAttributeName", Value = "type" },
                    new() { Name = "carValueToMatch", Value = "Golf" }
                }
            },
            Container = mock.Object
        };

        var streetLayer = new StreetLayer { Environment = FourNodeGraphEnv.GraphEnvironment };
        var carRentalLayer = new CarRentalLayer { StreetLayer = streetLayer };
        carRentalLayer.InitLayer(mapping, (_, _) => { }, (_, _) => { });

        CarRentalLayer = carRentalLayer;
    }

    public FourNodeGraphEnv FourNodeGraphEnv { get; }
    public ICarRentalLayer CarRentalLayer { get; }
}