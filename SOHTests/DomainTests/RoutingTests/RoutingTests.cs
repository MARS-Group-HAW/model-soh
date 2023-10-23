using System;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using Xunit;

namespace SOHTests.DomainTests.RoutingTests;

public class RoutingTests
{
    private readonly CarLayer _carLayer;
    private readonly ISpatialGraphEnvironment _environment;

    public RoutingTests()
    {
        var dataTableBicycle = CsvReader.MapData(ResourcesConstants.BicycleCsv);
        var dataTableCar = CsvReader.MapData(ResourcesConstants.CarCsv);
        var manager =
            new EntityManagerImpl((typeof(Bicycle), dataTableBicycle), (typeof(Car), dataTableCar));

        _carLayer = new CarLayer();
        var initData = new LayerInitData
            { LayerInitConfig = { File = ResourcesConstants.DriveGraphAltonaAltstadt } };
        _carLayer.InitLayer(initData, (_, _) => { }, (_, _) => { });

        _carLayer.EntityManager = manager;
        _environment = _carLayer.Environment;
    }

    [Fact]
    public void TravelTimeHeuristicForSlowVsFastCar()
    {
        var start = _environment.NearestNode(Position.CreateGeoPosition(9.954986699999999, 53.56093));
        var goal = _environment.NearestNode(Position.CreateGeoPosition(9.9360853, 53.5503159));
        Assert.InRange(start.Position.DistanceInKmTo(goal.Position), 1, 2);

        var slowCar = _carLayer.EntityManager.Create<Car>("type", "Golf");
        slowCar.Environment = _environment;
        slowCar.MaxSpeed = 30 / 3.6;

        var fastCar = _carLayer.EntityManager.Create<Car>("type", "Golf");
        fastCar.Environment = _environment;
        fastCar.MaxSpeed = 50 / 3.6;

        var routeWithSlowCar = _environment.FindRoute(start, goal, TravelTimeHeuristicFor(slowCar.MaxSpeed));
        var routeWithFastCar = _environment.FindRoute(start, goal, TravelTimeHeuristicFor(fastCar.MaxSpeed));

        Assert.NotEqual(routeWithSlowCar, routeWithFastCar);
    }

    /// <summary>
    ///     Travel time heuristic, can be used in conjunction with route planning.
    /// </summary>
    /// <param name="vehicleMaxSpeed">The max speed of the entity in m/s</param>
    /// <returns>Returns a new heuristic to distinguish <see cref="ISpatialEdge" /> according to the time to pass them.</returns>
    public static Func<ISpatialNode, ISpatialEdge, ISpatialNode, double> TravelTimeHeuristicFor(
        double vehicleMaxSpeed)
    {
        return (_, edge, _) => edge.Length / Math.Min(edge.MaxSpeed, vehicleMaxSpeed);
    } //TODO in IRoutePlanner ziehen?
}