using Mars.Interfaces.Environments;
using SOHModel.Car.Model;
using SOHModel.Car.Steering;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.DomainTests.SteeringTests;

public class FindEntityAheadTest
{
    private const int ExplorationDistance = 1000;
    private readonly Golf _car;

    // private readonly CarLayer _carLayer;
    private readonly CarSteeringHandle _carSteeringHandle;
    private readonly FourNodeGraphEnv _fourNodeGraphEnv;
    private readonly Route _route;

    public FindEntityAheadTest()
    {
        _fourNodeGraphEnv = new FourNodeGraphEnv();

        _car = Golf.Create(Environment);
        _carSteeringHandle = new CarSteeringHandle(Environment, _car);
        _route = Environment.FindRoute(_fourNodeGraphEnv.Node1, _fourNodeGraphEnv.Node4,
            (_, edge, _) => edge.Length);
        foreach (var stop in _route.Stops) stop.DesiredLane = 0;
    }

    private ISpatialGraphEnvironment Environment => _fourNodeGraphEnv.GraphEnvironment;

    [Fact]
    public void TestCarOnNodeEntityAheadOnFirstEdge()
    {
        var entityAhead = Golf.Create(Environment);
        var edge1 = _route[0].Edge;
        const int posEntityAhead = 10;
        Assert.True(Environment.Insert(entityAhead, edge1, posEntityAhead));
        Assert.True(Environment.Insert(_car, _fourNodeGraphEnv.Node1));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Equal(entityAhead, spatialGraphEntity);
        Assert.Equal(posEntityAhead, distance);
    }

    [Fact]
    public void TestCarOnSameEdgeSameLanePos0()
    {
        var entityAhead = Golf.Create(Environment);
        const int posEntityAhead = 10;
        var edge1 = _route[0].Edge;

        Assert.True(Environment.Insert(entityAhead, edge1, posEntityAhead));
        Assert.True(Environment.Insert(_car, edge1));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Equal(entityAhead, spatialGraphEntity);
        Assert.Equal(posEntityAhead, distance);
    }

    [Fact]
    public void TestCarOnSameEdgeSameLane()
    {
        const int posCar = 5;
        const int posEntityAhead = 10;

        var entityAhead = Golf.Create(Environment);
        var edge1 = _route[0].Edge;

        Assert.True(Environment.Insert(entityAhead, edge1, posEntityAhead));
        Assert.True(Environment.Insert(_car, edge1, posCar));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Equal(entityAhead, spatialGraphEntity);
        Assert.Equal(posEntityAhead - posCar, distance);
    }

    [Fact]
    public void TestEntityOnNextEdgeSameLane()
    {
        const int posCar = 5;
        const int posEntityAhead = 10;

        var entityAhead = Golf.Create(Environment);
        var edge1 = _route[0].Edge;
        var edge2 = _route[1].Edge;

        Assert.True(Environment.Insert(entityAhead, edge2, posEntityAhead));
        Assert.True(Environment.Insert(_car, edge1, posCar));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Equal(entityAhead, spatialGraphEntity);
        Assert.Equal(edge1.Length - posCar + posEntityAhead, distance);
    }

    [Fact]
    public void TestEntityOnNextEdgeOtherLane()
    {
        const int posCar = 5;
        const int posEntityAhead = 10;

        var entityAhead = Golf.Create(Environment);
        var edge1 = _route[0].Edge;
        var edge2 = _route[1].Edge;

        Assert.True(Environment.Insert(entityAhead, edge2, posEntityAhead, 1));
        Assert.True(Environment.Insert(_car, edge1, posCar));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Null(spatialGraphEntity);
        Assert.Equal(0, distance);
    }

    [Fact]
    public void TestEntityOnSameEdgeOtherLane()
    {
        const int posCar = 5;
        const int posEntityAhead = 10;

        var entityAhead = Golf.Create(Environment);
        var edge1 = _route[0].Edge;

        Assert.True(Environment.Insert(entityAhead, edge1, posEntityAhead, 1));
        Assert.True(Environment.Insert(_car, edge1, posCar));

        var spatialGraphExploreResult = Environment.Explore(_car, _route, ExplorationDistance);
        Assert.NotNull(spatialGraphExploreResult);

        var (spatialGraphEntity, distance) = _carSteeringHandle.FindEntityAhead(spatialGraphExploreResult, _route);
        Assert.Null(spatialGraphEntity);
        Assert.Equal(0, distance);
    }
}