using Mars.Interfaces.Environments;
using SOHCarModel.Model;
using SOHCarModel.Rental;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.CarModelTests.RentalLayerTests;

public class CarRentalLayerTests
{
    private readonly FourNodeCarRentalLayerFixture _fixture;

    public CarRentalLayerTests()
    {
        _fixture = new FourNodeCarRentalLayerFixture();
    }

    private ICarRentalLayer CarRentalLayer => _fixture.CarRentalLayer;

    [Fact]
    public void TestCarRentalLayerInitializedWithTwoVehicles()
    {
        var fourNodes = _fixture.FourNodeGraphEnv;
        var nearestCarToNode1 = CarRentalLayer.Nearest(fourNodes.Node1.Position);
        Assert.NotNull(nearestCarToNode1);
        Assert.Equal(fourNodes.Node2.Position, nearestCarToNode1.Position);

        var nearestCarToNode2 = CarRentalLayer.Nearest(fourNodes.Node2.Position);
        Assert.NotNull(nearestCarToNode2);
        Assert.Equal(fourNodes.Node2.Position, nearestCarToNode2.Position);

        var nearestCarToNode3 = CarRentalLayer.Nearest(fourNodes.Node3.Position);
        Assert.NotNull(nearestCarToNode3);
        Assert.Equal(fourNodes.Node3.Position, nearestCarToNode3.Position);

        var nearestCarToNode4 = CarRentalLayer.Nearest(fourNodes.Node4.Position);
        Assert.NotNull(nearestCarToNode4);
        Assert.Equal(fourNodes.Node3.Position, nearestCarToNode4.Position);
    }

    [Fact]
    public void TestCreateAdditionalRentalCar()
    {
        var start = _fixture.FourNodeGraphEnv.Node1.Position;
        var nearestCar = CarRentalLayer.Nearest(start);
        Assert.NotNull(nearestCar);
        Assert.NotEqual(start, nearestCar.Position);

        var rentalCar = new RentalCar { Position = start };
        Assert.True(CarRentalLayer.Insert(rentalCar));

        var nowNearestCar = CarRentalLayer.Nearest(start);
        Assert.NotNull(nowNearestCar);
        Assert.Equal(start, nowNearestCar.Position);
    }

    [Fact]
    public void TestRemoveRentalCar()
    {
        var start = _fixture.FourNodeGraphEnv.Node1.Position;
        var nearestCar = CarRentalLayer.Nearest(start);
        Assert.NotNull(nearestCar);

        Assert.True(CarRentalLayer.Remove(nearestCar));

        var nearestCar2 = CarRentalLayer.Nearest(start);
        Assert.NotNull(nearestCar2);
        Assert.NotEqual(nearestCar, nearestCar2);
    }

    [Fact]
    public void TestModalChoiceDefinition()
    {
        Assert.Equal(ModalChoice.CarRentalDriving, new CarRentalLayer().ModalChoice);
    }
}