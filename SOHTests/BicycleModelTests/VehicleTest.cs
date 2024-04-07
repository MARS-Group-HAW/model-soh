using System.Collections.Generic;
using System.Threading.Tasks;
using Mars.Common.Core.Random;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Numerics.Statistics;
using Moq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SOHModel.Bicycle.Common;
using SOHModel.Bicycle.Model;
using SOHModel.Bicycle.Rental;
using SOHModel.Bicycle.Steering;
using SOHModel.Domain.Graph;
using SOHModel.Domain.Steering.Common;
using Xunit;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHTests.BicycleModelTests;

public partial class VehicleTest
{
    [Fact]
    public void RentBicycleParallel()
    {
        var entityManager = new Mock<IEntityManager>();
        entityManager.Setup(manager => manager.Create<RentalBicycle>("type", "city", null))
            .Returns(() => new RentalBicycle());

        var bicycleRentalLayer = new BicycleRentalLayer();
        var container = new Mock<ISimulationContainer>();
        container.Setup(simulationContainer => simulationContainer.Resolve<IEntityManager>())
            .Returns(entityManager.Object);

        var layerInitData = new LayerInitData
        {
            Container = container.Object
        };

        bicycleRentalLayer.InitLayer(layerInitData, (_, _) => { }, (_, _) => { });
        bicycleRentalLayer.SpatialGraphMediatorLayer = new SpatialGraphMediatorLayer
        {
            Environment = new SpatialGraphEnvironment()
        };

        var attributes = new Dictionary<string, object> { { "Anzahl", 100 }, { "name", "Hbf" } };

        var bicycleRentalStation = new BicycleRentalStation();
        bicycleRentalStation.Init(bicycleRentalLayer,
            new VectorStructuredData
            {
                Geometry = new Point(0, 0),
                Attributes = new AttributesTable(attributes)
            });
        Assert.Equal(100, bicycleRentalStation.Count);

        Parallel.For(0, 100, _ => { Assert.NotNull(bicycleRentalStation.RentAny()); });

        Assert.Null(bicycleRentalStation.RentAny());
    }
}

internal class TestBicycleDriver : IBicycleSteeringCapable
{
    public Bicycle Bicycle { get; set; }

    public bool OvertakingActivated => false;

    public bool BrakingActivated
    {
        get => false;
        set { }
    }

    public Position Position { get; set; }

    public void Notify(PassengerMessage passengerMessage)
    {
        if (Bicycle != null && passengerMessage.Equals(PassengerMessage.NoDriver))
        {
            //todo
//                Assert.Null(Vehicle. Driver);
        }
    }

    public double DriverRandom => HandleDriverType.DetermineDriverRand(DriverType);
    public DriverType DriverType => DriverType.Normal;

    public double CyclingPower { get; } = new FastGaussianDistribution(75, 3)
        .Next(RandomHelper.Random);

    public double Mass { get; } = 80;
    public double Gradient { get; } = 0;
}

internal class TestBicycle : Bicycle
{
    public TestBicycle(int passengerCapacity)
    {
        PassengerCapacity = passengerCapacity;
    }
}