using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using SOHDomain.Graph;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHTests.MultimodalModelTests.RoutingTests;

public class RouteTests
{
    private readonly TestMultimodalLayer _multimodalLayer;

    private readonly SpatialGraphEnvironment _street;

    public RouteTests()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.DriveGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving }
                    }
                },
                new()
                {
                    File = ResourcesConstants.WalkGraphAltonaAltstadt,
                    InputConfiguration = new InputConfiguration
                    {
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                }
            }
        });

        _street = environment;

        var carParkingLayer = new CarParkingLayerFixture(new StreetLayer { Environment = _street }).CarParkingLayer;
        _multimodalLayer = new TestMultimodalLayer(_street)
        {
            CarParkingLayer = carParkingLayer
        };
    }

    private Route FindRouteByHops(ISpatialNode startNode, ISpatialNode goalNode)
    {
        var route = _street.FindRoute(startNode, goalNode);

        if (route == null)
        {
            const int hops = 3;
            foreach (var tempStartNode in _street.NearestNodes(startNode.Position, double.MaxValue, hops))
            {
                foreach (var tempGoalNode in _street.NearestNodes(goalNode.Position, double.MaxValue, hops))
                {
                    route = _street.FindRoute(tempStartNode, tempGoalNode);
                    if (route != null) break;
                }

                if (route != null) break;
            }
        }

        return route;
    }

    [Fact]
    public void TestRouteAlongEnvironment()
    {
        var sidewalk = new SpatialGraphEnvironment(new Input
        {
            File = ResourcesConstants.WalkGraphAltonaAltstadt,
            InputConfiguration = new InputConfiguration
            {
                IsBiDirectedImport = true
            }
        });

        var box = sidewalk.BoundingBox;


        var ring = new LinearRing(new[]
        {
            new Coordinate(box.MinX, box.MinY),
            new Coordinate(box.MinX, box.MaxY),
            new Coordinate(box.MaxX, box.MaxY),
            new Coordinate(box.MaxX, box.MinY),
            new Coordinate(box.MinX, box.MinY)
        });

        var routes = new List<Route>();
        for (var i = 0; i < 30; i++)
        {
            var source = ring.RandomPositionFromGeometry();
            var target = ring.RandomPositionFromGeometry();
            var route = sidewalk.FindShortestRoute(sidewalk.NearestNode(source), _street.NearestNode(target));
            Assert.NotNull(route);
            routes.Add(route);
        }

        var collection = new FeatureCollection();

        collection.AddRange(routes.Select((route, i) =>
        {
            var feature = route.ToFeature();
            feature.Attributes.Add("routeId", i);
            return feature;
        }));
    }

    [Fact]
    public void CheckRouteLengthForDistanceComparison()
    {
        var start = Position.CreateGeoPosition(9.9494707, 53.5611236);
        var goal = Position.CreateGeoPosition(9.9325976, 53.5447923);

        var distance = start.DistanceInMTo(goal);

        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CarDriving
        };
        agent.Init(_multimodalLayer);

        var multimodalRoute = agent.MultimodalRoute;

        Assert.InRange(distance, 0, multimodalRoute.RouteLength);
    }

    [Fact]
    public void CheckSpecificCarRoute()
    {
        var start = Position.CreatePosition(9.9511281, 53.5464907);
        var goal = Position.CreatePosition(9.9472114, 53.5612867);
        // var start = Position.CreateGeoPosition(9.9384069, 53.5522083);
        // var goal = Position.CreateGeoPosition(9.9472114, 53.5612867);
        // var startLatLon = start.ToLatLonString();
        // var goalLatLon = goal.ToLatLonString();

        // cannot find direct route
        var startNode = _street.NearestNode(start);
        var goalNode = _street.NearestNode(goal);
        Assert.NotEqual(startNode, goalNode);

        var carRoute = _street.FindShortestRoute(startNode, goalNode,
            edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));

        Assert.Null(carRoute);

        // carRoute =  FindRouteByHops(startNode, goalNode);
        // Assert.NotNull(carRoute);

        //try to find multimodal route
        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CarDriving
        };
        agent.Init(_multimodalLayer);

        var multimodalRoute = agent.MultimodalRoute;

        var distance = start.DistanceInMTo(goal);
        Assert.InRange(distance, 0, multimodalRoute.RouteLength);
    }

    [Fact]
    public void FindRouteByCheckingNearbyNodes()
    {
        var start = Position.CreateGeoPosition(9.9348281, 53.5477416);
        var goal = Position.CreateGeoPosition(9.9511738, 53.5463914);
        var startNode = _street.NearestNode(start);
        var goalNode = _street.NearestNode(goal);

        var route = FindRouteByHops(startNode, goalNode);

        Assert.NotNull(route);
    }
}