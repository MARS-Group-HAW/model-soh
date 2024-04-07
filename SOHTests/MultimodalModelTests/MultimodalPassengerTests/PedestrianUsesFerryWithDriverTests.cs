using System.Collections.Generic;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Ferry.Model;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class PedestrianUsesFerryWithDriverTests : IClassFixture<FerryRouteLayerFixture>
{
    private readonly FerryLayer _ferryLayer;
    private readonly TestMultimodalLayer _travelerLayer;

    public PedestrianUsesFerryWithDriverTests(FerryRouteLayerFixture routeLayerFixture)
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
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.ShipDriving }
                    }
                }
            }
        });

        _travelerLayer = new TestMultimodalLayer(environment)
        {
            FerryStationLayer = routeLayerFixture.FerryStationLayer
        };

        _ferryLayer = new FerryLayer(routeLayerFixture.FerryRouteLayer)
        {
            Context = _travelerLayer.Context,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.FerryCsv)),
            GraphEnvironment = environment
        };
    }

    [Fact]
    public void UseFerryAndWalkToGoal()
    {
        var start = Position.CreateGeoPosition(9.97101959, 53.54489498); //Landungsbrücken
        var goal = Position.CreateGeoPosition(9.94951, 53.53170); //Container Terminal Tollerort

        var dockWorker = new TestPassengerPedestrian
        {
            StartPosition = start
        };
        dockWorker.Init(_travelerLayer);
        dockWorker.MultimodalRoute = _travelerLayer.Search(dockWorker, start, goal, ModalChoice.Ferry);

        var driver = new FerryDriver(_ferryLayer, (_, _) => { })
        {
            Line = 61,
            MinimumBoardingTimeInSeconds = 20
        };

        Assert.False(dockWorker.HasUsedFerry);
        for (var tick = 0; tick < 2000; tick++, _travelerLayer.Context.UpdateStep())
        {
            driver.Tick();
            dockWorker.Tick();
        }

        Assert.True(dockWorker.HasUsedFerry);
        Assert.True(dockWorker.GoalReached);
    }
}