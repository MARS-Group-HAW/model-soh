using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using SOHTrainModel.Model;
using SOHTrainModel.Station;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class TrainCapacityTests
{
    private readonly TestMultimodalLayer _multimodalLayer;
    private readonly TrainLayer _trainLayer;
    private readonly TrainStationLayer _trainStationLayer;

    public TrainCapacityTests()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.HamburgRailStationAreasDriveGraph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.TrainU1Graph,
                    InputConfiguration = new InputConfiguration
                    {
                        NodeIntegrationKind = NodeIntegrationKind.MergeNode,
                        NodeToleranceInMeter = 10,
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.TrainDriving }
                    }
                }
            }
        });

        var routeLayerFixture = new TrainRouteLayerFixture();
        _trainStationLayer = routeLayerFixture.TrainRouteLayer.TrainStationLayer;

        _multimodalLayer = new TestMultimodalLayer(environment)
        {
            TrainStationLayer = _trainStationLayer
        };

        _trainLayer = new TrainLayer(routeLayerFixture.TrainRouteLayer)
        {
            Context = _multimodalLayer.Context,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.TrainCsv)),
            GraphEnvironment = environment
        };
    }

    [Fact]
    public void PassengerCapacityOfTrainExceeded()
    {
        var start = Position.CreateGeoPosition(10.0010703, 53.6769356); //Ochsenzoll
        var goal = Position.CreateGeoPosition(10.033216, 53.628261); //Klein Borstel

        var station = _trainStationLayer.Nearest(start);
        Assert.Equal("Ochsenzoll", station.Name);

        var driver = new TrainDriver(_trainLayer, (_, _) => { })
        {
            Line = "U1",
            MinimumBoardingTimeInSeconds = 10
        };
        driver.Train.Position = driver.Position;
        driver.Tick();
        Assert.Single(station.Trains);

        var capacity = driver.Train.PassengerCapacity;
        Assert.Equal(336, capacity);


        Assert.True(driver.Train.HasFreeCapacity());
        for (var i = 0; i < capacity; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Train);
            Assert.Equal(ModalChoice.Train, agent.MultimodalRoute.MainModalChoice);
            agent.Tick();
            Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        }

        Assert.False(driver.Train.HasFreeCapacity());
        for (var i = 0; i < capacity; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Train);
            agent.Tick();
            Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        }
    }

    [Fact]
    public void PassengerUseDifferentTrainsDueToCapacityLimitation()
    {
        var agents = new List<TestPassengerPedestrian>();
        var trainDrivers = new List<TrainDriver>();

        var start = Position.CreateGeoPosition(10.0010703, 53.6769356); //Ochsenzoll
        var goal = Position.CreateGeoPosition(10.033216, 53.628261); //Klein Borstel

        var usedTrains = new HashSet<Train>();

        const int agentCount = 400;
        for (var i = 0; i < agentCount; i++)
        {
            var agent = new TestPassengerPedestrian
            {
                StartPosition = start
            };
            agent.Init(_multimodalLayer);
            agent.MultimodalRoute = _multimodalLayer.Search(agent, start, goal, ModalChoice.Train);
            agents.Add(agent);
        }

        long firstGoalReachedTick = -1;
        const int spawningInterval = 300;
        const int ticks = 4000;
        for (var tick = 0;
             tick < ticks && !agents.All(agent => agent.GoalReached);
             tick++, _multimodalLayer.Context.UpdateStep())
        {
            foreach (var driver in trainDrivers) driver.Tick();
            foreach (var agent in agents)
            {
                agent.Tick();
                if (agent.UsedTrain != null) usedTrains.Add(agent.UsedTrain);
                if (agent.GoalReached && firstGoalReachedTick < 0)
                    firstGoalReachedTick = _multimodalLayer.Context.CurrentTick;
            }

            if (tick % spawningInterval == 0)
            {
                var driver = new TrainDriver(_trainLayer, (_, _) => { })
                {
                    Line = "U1",
                    MinimumBoardingTimeInSeconds = 20
                };
                trainDrivers.Add(driver);
            }
        }

        Assert.All(agents, pedestrian => Assert.True(pedestrian.GoalReached));

        const double variance = 0.85;

        Assert.Equal(2, usedTrains.Count);
        Assert.InRange(_multimodalLayer.Context.CurrentTick, firstGoalReachedTick * variance + spawningInterval,
            ticks);
    }
}