using System.Collections.Generic;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Station;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class PedestrianEntersCorrectFerryLineTests : IClassFixture<FerryRouteLayerFixture>
{
    private readonly FerryLayer _ferryLayer;
    private readonly FerryStationLayer _ferryStationLayer;
    private readonly TestMultimodalLayer _layer;
    private readonly Position _start, _goal;

    public PedestrianEntersCorrectFerryLineTests(FerryRouteLayerFixture routeLayerFixture)
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.FerryGraph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.ShipDriving }
                    }
                }
            }
        });

        _ferryStationLayer = routeLayerFixture.FerryStationLayer;


        var node1 = environment.AddNode(9.92223, 53.54907);
        var node2 = environment.AddNode(9.92235, 53.55067);
        var node3 = environment.AddNode(9.91261, 53.55113);
        var node4 = environment.AddNode(9.912405, 53.550753);

        //Edges
        var e1 = environment.AddEdge(node1, node2,
            new Dictionary<string, object> { { "lanes", 3 } });
        AddModalities(e1);
        var e2 = environment.AddEdge(node2, node1,
            new Dictionary<string, object> { { "lanes", 3 } });
        AddModalities(e2);
        var e3 = environment.AddEdge(node2, node3,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e3);
        var e4 = environment.AddEdge(node3, node2,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e4);
        var e5 = environment.AddEdge(node3, node4,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e5);
        var e6 = environment.AddEdge(node4, node3,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e6);

        _layer = new TestMultimodalLayer(environment)
        {
            FerryStationLayer = _ferryStationLayer
        };
        _start = Position.CreateGeoPosition(9.97114, 53.54484); //Landungsbrücken
        _goal = Position.CreateGeoPosition(9.914523, 53.543535); //Neumühlen/Övelgönne

        _ferryLayer = new FerryLayer(routeLayerFixture.FerryRouteLayer)
        {
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.FerryCsv)),
            GraphEnvironment = environment
        };
    }

    private static void AddModalities(ISpatialEdge edge)
    {
        edge.Modalities.Add(SpatialModalityType.Walking);
        edge.Modalities.Add(SpatialModalityType.Cycling);
        edge.Modalities.Add(SpatialModalityType.CarDriving);
    }

    [Fact]
    public void EnterFerryWithCorrectFerryLine()
    {
        var agent = new TestPassengerPedestrian //requires ferry line 62 to reach goal
        {
            StartPosition = _start
        };
        agent.Init(_layer);
        agent.MultimodalRoute = _layer.Search(agent, _start, _goal, ModalChoice.Ferry);
        Assert.Equal(1, agent.MultimodalRoute.Count);
        Assert.Equal(ModalChoice.Ferry, agent.MultimodalRoute.CurrentModalChoice);

        var ferryStation = _ferryStationLayer.Nearest(_start);
        Assert.Equal("Landungsbrücken Brücke 1", ferryStation.Name);

        agent.Tick();
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        Assert.Empty(ferryStation.Ferries);
        var ferryLine61driver = new FerryDriver(_ferryLayer, (_, _) => { })
        {
            Line = 61,
            MinimumBoardingTimeInSeconds = 10
        };
        ferryLine61driver.Ferry.Position = ferryLine61driver.Position;
        ferryLine61driver.Tick();
        Assert.Single(ferryStation.Ferries);

        agent.Tick();
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        Assert.True(ferryStation.Leave(ferryLine61driver.Ferry));
        Assert.Empty(ferryStation.Ferries);
        var ferryLine72driver = new FerryDriver(_ferryLayer, (_, _) => { })
        {
            Line = 72,
            MinimumBoardingTimeInSeconds = 10
        };
        ferryLine72driver.Ferry.Position = ferryLine72driver.Position;
        ferryLine72driver.Tick();
        Assert.Single(ferryStation.Ferries);

        agent.Tick();
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        Assert.True(ferryStation.Leave(ferryLine72driver.Ferry));
        Assert.Empty(ferryStation.Ferries);
        var ferryLine62driver = new FerryDriver(_ferryLayer, (_, _) => { })
        {
            Line = 62,
            MinimumBoardingTimeInSeconds = 10
        };
        ferryLine62driver.Ferry.Position = ferryLine62driver.Position;
        ferryLine62driver.Tick();
        Assert.Single(ferryStation.Ferries);

        agent.Tick();
        Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
    }
}