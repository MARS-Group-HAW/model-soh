using System;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Moq;
using SOHCarModel.Parking;
using SOHDomain.Graph;

namespace SOHTests.Commons.Layer;

/// <summary>
///     Holds the car parking layer for Altona Altstadt
/// </summary>
public class CarParkingLayerFixture : IDisposable
{
    public CarParkingLayerFixture(ISpatialGraphLayer streetLayer)
    {
        var dataTable = CsvReader.MapData(ResourcesConstants.CarCsv);
        var manager = new EntityManagerImpl(dataTable);

        var mock = new Mock<ISimulationContainer>();
        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(manager);
        var layerInitData = new LayerInitData
        {
            LayerInitConfig = { File = ResourcesConstants.ParkingAltonaAltstadt },
            Container = mock.Object
        };

        CarParkingLayer = new CarParkingLayer { StreetLayer = streetLayer };
        CarParkingLayer.InitLayer(layerInitData);
    }

    public CarParkingLayer CarParkingLayer { get; }

    public void Dispose()
    {
        CarParkingLayer.Dispose();
    }
}