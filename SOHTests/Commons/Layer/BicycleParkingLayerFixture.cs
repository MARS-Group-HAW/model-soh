using System;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Moq;
using SOHBicycleModel.Parking;
using SOHDomain.Graph;

namespace SOHTests.Commons.Layer;

/// <summary>
///     Holds the bicycle parking layer for Altona Altstadt
/// </summary>
public class BicycleParkingLayerFixture : IDisposable
{
    public BicycleParkingLayerFixture(ISpatialGraphEnvironment environment)
    {
        var dataTable = CsvReader.MapData(ResourcesConstants.BicycleCsv);
        var manager = new EntityManagerImpl(dataTable);

        var mock = new Mock<ISimulationContainer>();
        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(manager);
        var layerInitData = new LayerInitData
        {
            LayerInitConfig = { File = ResourcesConstants.ParkingAltonaAltstadt },
            Container = mock.Object
        };

        var mediatorLayer = new SpatialGraphMediatorLayer { Environment = environment };
        BicycleParkingLayer = new BicycleParkingLayer { GraphLayer = mediatorLayer };
        BicycleParkingLayer.InitLayer(layerInitData);
    }

    public BicycleParkingLayer BicycleParkingLayer { get; }

    public void Dispose()
    {
        BicycleParkingLayer.Dispose();
    }
}