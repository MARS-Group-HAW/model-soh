using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Rental;
using SOHMultimodalModel.Multimodal;
using SOHMultimodalModel.Routing;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.RoutingTests;

/// <summary>
///     Tests explicitly the <see cref="WalkingCyclingMultimodalRoute" />
/// </summary>
public class WalkingCyclingMultimodalRouteTests
{
    private readonly MultimodalAgent<TestMultimodalLayer> _agent;
    private readonly BicycleRentalLayer _bicycleRentalLayer;
    private readonly MultimodalRouteFinder _routeFinder;
    private readonly Position _start, _goal;

    public WalkingCyclingMultimodalRouteTests()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        _bicycleRentalLayer = new BicycleRentalLayerFixture(environment).BicycleRentalLayer;

        var layer = new TestMultimodalLayer(environment)
        {
            BicycleRentalLayer = _bicycleRentalLayer
        };

        _routeFinder = new MultimodalRouteFinder(layer.SpatialGraphMediatorLayer);

        _start = Position.CreateGeoPosition(9.9525048, 53.5565105);
        _goal = Position.CreateGeoPosition(9.9425878, 53.5461201);
        _agent = new TestMultiCapableAgent
        {
            StartPosition = _start,
            CapabilityCycling = true
        };
        _agent.Init(layer);
    }

    [Fact]
    public void FindWalkCycleRoute()
    {
        var multimodalRoute = _routeFinder.Search(_agent, _start, _goal, ModalChoice.CyclingRentalBike);
        Assert.NotNull(multimodalRoute);
        Assert.Equal(3, multimodalRoute.Count);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.Stops[0].ModalChoice);
        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.Stops[1].ModalChoice);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.Stops[2].ModalChoice);
        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.MainModalChoice);

        Assert.All(multimodalRoute.Stops, stop => Assert.NotEqual(0, stop.Route.Count));
    }

    [Fact]
    public void NoRentalBicyclesAvailable()
    {
        BicycleRentalStation rentalStation;
        while ((rentalStation = _bicycleRentalLayer.Nearest(_start, true)) != null)
        {
            var rentalBicycle = rentalStation.RentAny();
            Assert.True(rentalStation.Leave(rentalBicycle));
        }

        var multimodalRoute = _routeFinder.Search(_agent, _start, _goal, ModalChoice.CyclingRentalBike);
        Assert.NotNull(multimodalRoute);
        Assert.Equal(1, multimodalRoute.Count);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.Stops[0].ModalChoice);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.MainModalChoice);

        Assert.All(multimodalRoute.Stops, stop => Assert.NotEqual(0, stop.Route.Count));
    }
}