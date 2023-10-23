using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.RoutingTests;

public class WalkingFerryDrivingMultimodalRouteTests
{
    private readonly TestMultimodalLayer _layer;

    public WalkingFerryDrivingMultimodalRouteTests()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.FerryContainerWalkingGraph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.FerryGraph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        NodeIntegrationKind = NodeIntegrationKind.LinkNode,
                        NodeToleranceInMeter = 10,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.ShipDriving }
                    }
                }
            }
        });

        _layer = new TestMultimodalLayer(environment)
        {
            FerryStationLayer = new FerryRouteLayerFixture().FerryStationLayer
        };
    }

    [Fact]
    public void FindSingleRoute()
    {
        var start = Position.CreateGeoPosition(9.971040, 53.544898); //Landungsbrücken
        var goal = Position.CreateGeoPosition(9.940715, 53.524153); //Neuhof

        var env = _layer.StreetEnvironment;
        var route = env.FindShortestRoute(env.NearestNode(start), env.NearestNode(goal));
        Assert.NotNull(route);

        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(1, multimodalRoute.Count);
        Assert.Equal(ModalChoice.Ferry, multimodalRoute.MainModalChoice);
        Assert.NotEqual(0, multimodalRoute.First().Route.Count);
    }

    [Fact]
    public void FindRouteFromFerryStationToContainerTerminal()
    {
        var start = Position.CreateGeoPosition(9.97110, 53.54488); //Landungsbrücken
        var goal = Position.CreateGeoPosition(9.94951, 53.53170); //Container Terminal Tollerort

        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(2, multimodalRoute.Count);
        Assert.Equal(ModalChoice.Ferry, multimodalRoute.MainModalChoice);

        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route));
    }

    [Fact]
    public void FindWalkingRouteFromFerryStationToContainerTerminal()
    {
        var start = Position.CreateGeoPosition(9.9406296, 53.5241216); //Neuhof
        var goal = Position.CreateGeoPosition(9.94951, 53.53170); //Container Terminal Tollerort

        // walking route
        var env = _layer.SidewalkEnvironment;
        var route = env.FindShortestRoute(env.NearestNode(start), env.NearestNode(goal));
        Assert.NotNull(route);

        // multimodal walking route
        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Single(multimodalRoute);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.MainModalChoice);

        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route));
    }

    [Fact]
    public void FindRouteWithOneTransferPoint()
    {
        var start = Position.CreateGeoPosition(9.9152, 53.5431); //Neumühlen/Övelgönne
        var goal = Position.CreateGeoPosition(9.9373, 53.5256); //Waltershof

        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(3, multimodalRoute.Count);

        var stop0 = multimodalRoute[0];
        Assert.Equal(ModalChoice.Ferry, stop0.ModalChoice);
        Assert.NotEmpty(stop0.Route);

        var stop1 = multimodalRoute[1];
        Assert.Equal(ModalChoice.Ferry, stop1.ModalChoice);
        Assert.NotEmpty(stop1.Route);

        var stop2 = multimodalRoute[2];
        Assert.Equal(ModalChoice.Walking, stop2.ModalChoice);
        Assert.NotEmpty(stop2.Route);
    }

    [Fact]
    public void FindRouteWithTwoTransferPoints()
    {
        var start = Position.CreateGeoPosition(9.86303, 53.54633); //Teufelsbrück
        var goal = Position.CreateGeoPosition(9.9864370034622283, 53.540329937434237); //Elbphilharmonie
        TestRouteWithTwoTransferPoints(start, goal);

        start = Position.CreateGeoPosition(9.863409, 53.541612); //Rüschpark
        goal = Position.CreateGeoPosition(9.9760816, 53.5282128); //Argentinienbrücke
        TestRouteWithTwoTransferPoints(start, goal);

        start = Position.CreateGeoPosition(9.981920, 53.524097); //Ernst-August-Schleuse
        goal = Position.CreateGeoPosition(9.86303, 53.54633); //Teufelsbrück
        TestRouteWithTwoTransferPoints(start, goal);
    }

    private void TestRouteWithTwoTransferPoints(Position start, Position goal)
    {
        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(3, multimodalRoute.Count);

        Assert.All(multimodalRoute.Stops, stop =>
        {
            Assert.Equal(ModalChoice.Ferry, stop.ModalChoice);
            Assert.NotEmpty(stop.Route.Stops);
        });
    }

    [Fact]
    public void FindFerryStationThatAllowsWalkToGoal()
    {
        var start = Position.CreateGeoPosition(9.952234, 53.543938); //Altona (Fischmarkt)
        var goal = Position.CreateGeoPosition(9.93450960435, 53.51092628320); //Am Sandauhafen

        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(2, multimodalRoute.Count);
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route.Stops));

        //opposite direction
        multimodalRoute = _layer.Search(agent, goal, start, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(2, multimodalRoute.Count);
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route.Stops));
    }

    [Fact]
    public void FindFerryStationOnSameSideOfWater()
    {
        // var startE = Position.CreatePosition(9.9856891, 53.5429279); //near Elbphilharmonie
        // var goalE = Position.CreatePosition(9.9856891, 53.5429279); //near Elbphilharmonie

        // var start  = Position.CreatePosition(9.9718454, 53.5451125);
        var start = Position.CreatePosition(9.9856891, 53.5429279); //near Elbphilharmonie
        var goal = Position.CreatePosition(9.9146091, 53.544245); //near Övelgönne

        var agent = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        agent.Init(_layer);
        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);

        Assert.Equal(4, multimodalRoute.Count); //walk-ferry-ferry-walk (needs ferry transfer point)
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route.Stops));


        var stationPosition = Position.CreatePosition(9.986437003462228, 53.54032993743424); //Elbphilharmonie station
        Assert.Equal(stationPosition, multimodalRoute.Stops[0].Route.Goal);

        //opposite direction
        multimodalRoute = _layer.Search(agent, goal, start, ModalChoice.Ferry);
        Assert.NotEmpty(multimodalRoute);
        Assert.Equal(4, multimodalRoute.Count);
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route.Stops));
        Assert.Equal(stationPosition, multimodalRoute.Last().Route.Stops.First().Edge.From.Position);
    }
}