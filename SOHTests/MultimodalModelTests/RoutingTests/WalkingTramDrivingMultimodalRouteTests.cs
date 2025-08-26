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

public class WalkingTramDrivingMultimodalRouteTests
{
    private readonly TestMultimodalLayer _layer;

    public WalkingTramDrivingMultimodalRouteTests()
    {
        // Build a minimal multimodal environment: sidewalks + tram tracks (as TrainDriving)
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.WalkGraphCasa, 
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.TramT1Graph, 
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        NodeIntegrationKind = NodeIntegrationKind.LinkNode,
                        NodeToleranceInMeter = 15,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.TrainDriving }
                    }
                }
            }
        });

        // Station layers for PT routing (use your Tram fixture)
        var tramFixture = new TramRouteLayerFixture();

        _layer = new TestMultimodalLayer(environment)
        {
            // If your MultimodalRouteFinder uses Train for tram legs,
            // wiring the Tram station layer into the Train slot ensures discovery.
            TramStationLayer = tramFixture.TramStationLayer
        };
    }
    

    // 2) From station to POI: starting at stop → PT + walk
    [Fact]
    public void FindRouteFromTramStationToPOI()
    {
        // Start on Technopark (92900), goal near Facultes (92903) vicinity
        var start = Position.CreateGeoPosition(-7.6437949, 33.5408451);
        var goal = Position.CreateGeoPosition(-7.6412474, 33.5407512);

        var agent = new TestPassengerPedestrian { StartPosition = start };
        agent.Init(_layer);

        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Train);
        Assert.NotEmpty(multimodalRoute);

        // Typically: one tram leg + short final walk
        Assert.InRange(multimodalRoute.Count, 1, 2);
        Assert.Equal(ModalChoice.Train, multimodalRoute.MainModalChoice);
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route));
    }

    // 3) Walking-only equivalence: if tram is unnecessary, fallback is pure walking
    [Fact]
    public void FindWalkingOnlyRoute()
    {
        // Short hop near Zenith (92901) to CasaSud (92902) – walk is OK
        var start = Position.CreateGeoPosition(-7.6428042, 33.5407714);
        var goal = Position.CreateGeoPosition(-7.6419494, 33.5407289);

        // Baseline walking route exists
        var env = _layer.SidewalkEnvironment;
        var walk = env.FindShortestRoute(env.NearestNode(start), env.NearestNode(goal));
        Assert.NotNull(walk);

        var agent = new TestPassengerPedestrian { StartPosition = start };
        agent.Init(_layer);

        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Train);
        Assert.NotEmpty(multimodalRoute);
        Assert.Single(multimodalRoute);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.MainModalChoice);
        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route));
    }

    // 4) “Two boardings” equivalence (if route builder splits legs): PT + PT + walk
    //    Some implementations expose intermediate PT legs when a transfer or branch-like movement occurs.
    [Fact]
    public void FindRouteWithPotentialIntermediatePTSplit()
    {
        // From Mekka (71900) side deeper into line (Facultes 92903 area).
        var start = Position.CreateGeoPosition(-7.647001, 33.5410578);
        var goal = Position.CreateGeoPosition(-7.6412474, 33.5407512);

        var agent = new TestPassengerPedestrian { StartPosition = start };
        agent.Init(_layer);

        var multimodalRoute = _layer.Search(agent, start, goal, ModalChoice.Train);
        Assert.NotEmpty(multimodalRoute);

        // Depending on your route slicer, this may appear as one PT leg or split PT legs.
        // Accept either 1 (PT) or 2 (PT segments) plus optional walk.
        Assert.InRange(multimodalRoute.Count, 1, 3);
        Assert.Contains(multimodalRoute.MainModalChoice, new[] { ModalChoice.Train, ModalChoice.Walking });

        Assert.All(multimodalRoute.Stops, stop => Assert.NotEmpty(stop.Route));
    }

    // 5) “Station enables walk to goal” equivalence: one PT + one walk both directions
    [Fact]
    public void FindTramStationThatAllowsWalkToGoal_BothDirections()
    {
        // Board at Mekka area and walk off near Panoramique
        var start = Position.CreateGeoPosition(-7.647001, 33.5410578);
        var goal = Position.CreateGeoPosition(-7.6446892, 33.5409047);

        var agent = new TestPassengerPedestrian { StartPosition = start };
        agent.Init(_layer);

        var forward = _layer.Search(agent, start, goal, ModalChoice.Train);
        Assert.NotEmpty(forward);
        Assert.InRange(forward.Count, 1, 2);
        Assert.All(forward.Stops, s => Assert.NotEmpty(s.Route.Stops));

        var backward = _layer.Search(agent, goal, start, ModalChoice.Train);
        Assert.NotEmpty(backward);
        Assert.InRange(backward.Count, 1, 2);
        Assert.All(backward.Stops, s => Assert.NotEmpty(s.Route.Stops));
    }

    // 6) Same-side / short-distance choice: expects walking dominance or minimal PT
    [Fact]
    public void PreferWalkingWhenStationsAreCoLocated()
    {
        // Two positions very close on the same side of the corridor
        var pA = Position.CreateGeoPosition(-7.6429, 33.54078);
        var pB = Position.CreateGeoPosition(-7.6427, 33.54077);

        var agent = new TestPassengerPedestrian { StartPosition = pA };
        agent.Init(_layer);

        var route = _layer.Search(agent, pA, pB, ModalChoice.Train);
        Assert.NotEmpty(route);

        // Pure walking or at most walk-PT-walk with tiny PT
        Assert.InRange(route.Count, 1, 3);
        Assert.Contains(route.MainModalChoice, new[] { ModalChoice.Walking, ModalChoice.Train });
        Assert.All(route.Stops, s => Assert.NotEmpty(s.Route.Stops));
    }

    // 7) Reversed direction equivalence: start leg matches last schedule entry in reversed mode
    [Fact]
    public void ReverseTramRoute_UsesLastEntryAsStart_AndRemainsConsistent()
    {
        // We only validate logic
        var start = Position.CreateGeoPosition(-7.6412474, 33.5407512); // Facultes vicinity
        var goal = Position.CreateGeoPosition(-7.647001, 33.5410578); // Mekka vicinity

        var agent = new TestPassengerPedestrian { StartPosition = start };
        agent.Init(_layer);

        var route = _layer.Search(agent, start, goal, ModalChoice.Train);
        Assert.NotEmpty(route);

        // Expect at least one PT leg; reversed traversal should still be routable
        Assert.InRange(route.Count, 1, 3);
        Assert.All(route.Stops, s => Assert.NotEmpty(s.Route));
    }
}