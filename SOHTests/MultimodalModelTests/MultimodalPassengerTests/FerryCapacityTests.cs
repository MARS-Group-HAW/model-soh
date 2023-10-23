using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHFerryModel.Model;
using SOHFerryModel.Station;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class FerryCapacityTests
{
    private readonly FerryLayer _ferryLayer;
    private readonly FerryStationLayer _ferryStationLayer;
    private readonly TestMultimodalLayer _multimodalLayer;

    public FerryCapacityTests()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.WalkGraphAltonaAltstadt,
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
                        NodeIntegrationKind = NodeIntegrationKind.MergeNode,
                        NodeToleranceInMeter = 10,
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.ShipDriving }
                    }
                }
            }
        });

        var routeLayerFixture = new FerryRouteLayerFixture();
        _ferryStationLayer = routeLayerFixture.FerryRouteLayer.StationLayer;

        _multimodalLayer = new TestMultimodalLayer(environment)
        {
            FerryStationLayer = _ferryStationLayer
        };

        _ferryLayer = new FerryLayer(routeLayerFixture.FerryRouteLayer)
        {
            Context = _multimodalLayer.Context,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.FerryCsv)),
            GraphEnvironment = environment
        };
    }

    [Fact]
    public void PassengerCapacityOfFerryExceeded()
    {
        var start = Position.CreateGeoPosition(9.97114, 53.54484); //Landungsbrücken
        var goal = Position.CreateGeoPosition(9.9522322, 53.5439412); //Fischmarkt

        var ferryStation = _ferryStationLayer.Nearest(start);
        Assert.Equal("Landungsbrücken Brücke 1", ferryStation.Name);

        var driver = new FerryDriver(_ferryLayer, (_, _) => { })
        {
            Line = 62,
            MinimumBoardingTimeInSeconds = 10
        };
        driver.Ferry.Position = driver.Position;
        driver.Tick();
        Assert.Single(ferryStation.Ferries);

        var capacity = driver.Ferry.PassengerCapacity;
        Assert.Equal(250, capacity);

        Assert.True(driver.Ferry.HasFreeCapacity());
        for (var i = 0; i < capacity; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Ferry);
            Assert.Equal(ModalChoice.Ferry, agent.MultimodalRoute.MainModalChoice);
            agent.Tick();
            Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        }

        Assert.False(driver.Ferry.HasFreeCapacity());
        for (var i = 0; i < capacity; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Ferry);
            agent.Tick();
            Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        }
    }

    [Fact]
    public void PassengerUseDifferentFerriesDueToCapacityLimitation()
    {
        var agents = new List<TestPassengerPedestrian>();
        var ferryDriver = new List<FerryDriver>();

        var start = Position.CreateGeoPosition(9.97114, 53.54484); //Landungsbrücken
        var goal = Position.CreateGeoPosition(9.9505593, 53.5462456); //near Fischmarkt

        var usedFerries = new HashSet<Ferry>();

        const int agentCount = 400;
        for (var i = 0; i < agentCount; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Ferry);
            agents.Add(agent);
        }

        long firstGoalReachedTick = -1;
        const int spawningInterval = 300;
        const int ticks = 2000;
        for (var tick = 0;
             tick < ticks && !agents.All(agent => agent.GoalReached);
             tick++, _multimodalLayer.Context.UpdateStep())
        {
            foreach (var driver in ferryDriver) driver.Tick();
            foreach (var agent in agents)
            {
                agent.Tick();
                if (agent.UsedFerry != null) usedFerries.Add(agent.UsedFerry);
                if (agent.GoalReached && firstGoalReachedTick < 0)
                    firstGoalReachedTick = _multimodalLayer.Context.CurrentTick;
            }

            if (tick % spawningInterval == 0)
            {
                var driver = new FerryDriver(_ferryLayer, (_, _) => { })
                {
                    Line = 62,
                    MinimumBoardingTimeInSeconds = 20
                };
                ferryDriver.Add(driver);
            }
        }

        Assert.All(agents, pedestrian => Assert.True(pedestrian.GoalReached));

        const double variance = 0.85;

        Assert.Equal(2, usedFerries.Count);
        Assert.InRange(_multimodalLayer.Context.CurrentTick, firstGoalReachedTick * variance + spawningInterval,
            ticks);
    }
}