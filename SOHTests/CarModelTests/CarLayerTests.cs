using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Moq;
using SOHCarModel.Model;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.CarModelTests;

public class CarLayerTests
{
    [Fact]
    public void InitDataEnvironmentOverwritesConstructorEnvironment()
    {
        var environment = new FourNodeGraphEnv().GraphEnvironment;
        var carLayer = new CarLayer(environment);
        Assert.Equal(environment, carLayer.Environment);

        var dataTable = CsvReader.MapData(ResourcesConstants.CarCsv);
        var manager = new EntityManagerImpl(dataTable);

        var mock = new Mock<ISimulationContainer>();
        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(manager);
        var layerInitData = new LayerInitData(SimulationContext.Start2020InSeconds)
        {
            LayerInitConfig =
            {
                File = ResourcesConstants.DriveGraphAltonaAltstadt
            },
            Container = mock.Object
        };
        carLayer.InitLayer(layerInitData, (_, _) => { }, (_, _) => { });
        Assert.NotEqual(environment, carLayer.Environment);
    }

    [Fact]
    public void InitEnvironmentWithConstructor()
    {
        var carLayer = new CarLayer(new FourNodeGraphEnv().GraphEnvironment);
        Assert.NotNull(carLayer.Environment);
    }

    [Fact]
    public void InitEnvironmentWithInitData()
    {
        var carLayer = new CarLayer();
        Assert.Null(carLayer.Environment);

        var dataTable = CsvReader.MapData(ResourcesConstants.CarCsv);
        var manager = new EntityManagerImpl(dataTable);

        var mock = new Mock<ISimulationContainer>();
        mock.Setup(container => container.Resolve<IEntityManager>()).Returns(manager);
        var initData = new LayerInitData
        {
            LayerInitConfig =
            {
                File = ResourcesConstants.DriveGraphAltonaAltstadt
            },
            Container = mock.Object
        };
        carLayer.InitLayer(initData, (_, _) => { }, (_, _) => { });

        Assert.NotNull(carLayer.Environment);
    }
}