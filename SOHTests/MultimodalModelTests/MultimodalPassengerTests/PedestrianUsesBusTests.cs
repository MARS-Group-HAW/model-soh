using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using MongoDB.Driver.Linq;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class BusLayerFixture : AbstractBusLayerFixture
{
    public BusLayerFixture() : base(ResourcesConstants.Bus113LineCsv, ResourcesConstants.BusStations113)
    {

    }
}


public class PedestrianUsesBusTests : IClassFixture<BusLayerFixture>
{
    private readonly TestMultimodalLayer _multimodalLayer;
    private readonly BusLayer _busLayer;
    private readonly BusStationLayer _busStationLayer;

    private readonly BusLayerFixture _busLayerFixture;

    private ISpatialGraphEnvironment _environment;

    public PedestrianUsesBusTests(BusLayerFixture busLayerFixture)
    {
        _busLayerFixture = busLayerFixture;
        _environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.WalkGraphBus113Test,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.Bus113Graph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving }
                    }
                }
            }
        });

        _busStationLayer = _busLayerFixture.BusStationLayer;

        _multimodalLayer = new TestMultimodalLayer(_environment)
        {
            BusStationLayer = _busStationLayer
        };

        _busLayer = new BusLayer()
        {
            BusRouteLayer = _busLayerFixture.BusRouteLayer,
            Context = _multimodalLayer.Context,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.BusCsv)),
            GraphEnvironment = _environment
        };
    }


    /// <summary>
    ///     Tests that a pedestrian can enter a bus.
    /// </summary>
    /// <remarks>
    ///     This test sets up a scenario where a pedestrian starts at a specific position and has a goal position.
    ///     It finds the nearest bus stations to the start and goal positions, creates a bus route between these stations,
    ///     and initializes a bus driver with this route. The test then verifies that the bus has free capacity,
    ///     initializes a pedestrian agent, assigns a multimodal route to the agent, and checks that the agent
    ///     successfully enters the bus.
    /// </remarks>    
    [Fact]
    public void PedestrianEntersBus()
    {
        Position sourcePos = _environment.Nodes.First(node => node.OutgoingEdges.Any(edge => edge.Value.Modalities.Contains(SpatialModalityType.CarDriving))).Position;
        ISpatialNode sourceNode = _environment.NearestNode(sourcePos, outgoingModality: SpatialModalityType.CarDriving);
        Position targetPos;

        Route route = new();
        foreach (ISpatialNode node in _environment.Nodes) {
            var _route = _environment.FindShortestRoute(sourceNode, node, edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));
            if (_route != null && _route.Count > 0) {
                route = _route;
                break;
            }
        }
        Assert.NotEmpty(route);

        targetPos = route.Last().Edge.To.Position;

        
        BusDriver driver = new (_busLayer, (_, _) => {})
        {
            Line = "113",
            Position = sourcePos,
            BusRoute = new BusRoute
            {
                Entries = route.Select(stop => new BusRouteEntry(
                    new BusStation
                    {
                        Position = stop.Edge.From.Position
                    }, new BusStation
                    {
                        Position = stop.Edge.To.Position
                    }, 0)).ToList()
            }
        };

        driver.Tick();
        _busLayer.Context.UpdateStep();

        Assert.Equal(sourcePos, driver.Position);

        TestPassengerPedestrian agent = new ()
        {
            StartPosition = sourcePos
        };

        agent.Init(_multimodalLayer);

        // assert, that the agent is not yet in the bus
        Assert.NotEqual(Whereabouts.Vehicle, agent.Whereabouts);
        agent.TryEnterVehicleAsPassenger(driver.Bus, agent);
        // assert, that the agent entered the bus
        Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        Assert.Contains(agent, driver.Bus.Passengers);
    }


    /// <summary>
    /// Precondition: Have a passenger in a bus
    /// -> assert, that s/he is actually in the bus
    /// Action: leave the bus
    /// -> assert, that the passenger is not in the bus anymore
    /// -> assert, that the passengers' position is the same as the source bus stations' position 
    /// </summary>
    [Fact]
    public void PedestrianLeavesBus()
    {
        Position sourcePos = _environment.Nodes.First(node => node.OutgoingEdges.Any(edge => edge.Value.Modalities.Contains(SpatialModalityType.CarDriving))).Position;
        ISpatialNode sourceNode = _environment.NearestNode(sourcePos, outgoingModality: SpatialModalityType.CarDriving);
        Position targetPos;

        Route route = new();
        foreach (ISpatialNode node in _environment.Nodes) {
            var _route = _environment.FindShortestRoute(sourceNode, node, edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));
            if (_route != null && _route.Count > 0) {
                route = _route;
                break;
            }
        }
        Assert.NotEmpty(route);

        targetPos = route.Last().Edge.To.Position;

        
        bool goalReached = false;
        BusDriver driver = new (_busLayer, (_, _) => goalReached = true)
        {
            Line = "113",
            Position = sourcePos,
            BusRoute = new BusRoute
            {
                Entries = route.Select(stop => new BusRouteEntry(
                    new BusStation
                    {
                        Position = stop.Edge.From.Position
                    }, new BusStation
                    {
                        Position = stop.Edge.To.Position
                    }, 0)).ToList()
            }
        };

        driver.Tick();
        _busLayer.Context.UpdateStep();

        Assert.Equal(sourcePos, driver.Position);

        TestPassengerPedestrian agent = new ()
        {
            StartPosition = sourcePos
        };

        agent.Init(_multimodalLayer);
        agent.MultimodalRoute = _multimodalLayer.Search(agent, sourcePos, targetPos, ModalChoice.Bus);

        // assert, that the agent is not yet in the bus
        Assert.NotEqual(Whereabouts.Vehicle, agent.Whereabouts);

        // assert, that the bus and the agent are at the same position
        Assert.Equal(driver.Bus.Position, agent.Position);

        agent.TryEnterVehicleAsPassenger(driver.Bus, agent);
        
        // assert, that the agent entered the bus
        Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        Assert.Contains(agent, driver.Bus.Passengers);


        while (!goalReached) {
            agent.Tick();
            driver.Tick();
            _busLayer.Context.UpdateStep();
        }

        // assert, that agent and bus reached the target station
        Assert.Equal(agent.Position, driver.Bus.Position);
        Assert.Equal(agent.Position, targetPos);

        // assert, that the agent left the bus
        Assert.NotEqual(Whereabouts.Vehicle, agent.Whereabouts);
        Assert.DoesNotContain(agent, driver.Bus.Passengers);
    }
}