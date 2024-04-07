using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;
using Xunit;

namespace SOHTests.TrainModelTests;

public class TrainGtfsRoutesTests
{
    private readonly TrainLayer _layer;

    public TrainGtfsRoutesTests()
    {
        var trainStationLayer = new TrainStationLayer();
        trainStationLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = ResourcesConstants.TrainStationsU1 }
            });

        var routeGtfsRouteLayer = new TrainGtfsRouteLayer
        {
            TrainStationLayer = trainStationLayer
        };
        routeGtfsRouteLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig = { File = "res/entity_inits/HVV_GTFS" }
        });
        _layer = new TrainLayer(routeGtfsRouteLayer)
        {
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.TrainCsv)),
            GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.TrainU1Graph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true
                        }
                    }
                }
            })
        };
    }

    [Fact]
    public void VisitAllStationsAlongTrainRoute()
    {
        const string line = "U1";
        var driver = new TrainDriver(_layer, (_, _) => { })
        {
            Line = line,
            MinimumBoardingTimeInSeconds = 10
        };

        Assert.True(_layer.TrainRouteLayer.TryGetRoute(line, out var schedule));
        var unvisitedStationEntries = schedule.Entries.ToList();
        Assert.Equal(23, unvisitedStationEntries.Count);
        for (var tick = 0; tick < 9000; tick++, _layer.Context.UpdateStep())
        {
            driver.Tick();
            if (driver.Boarding)
            {
                var routeEntry = driver.TrainRouteEnumerator.Current;
                unvisitedStationEntries.Remove(routeEntry);
            }
        }

        Assert.Empty(unvisitedStationEntries);
    }

    [Fact]
    public void SufficientBoardingTimeAtStations()
    {
        const int minimalBoardingTime = 32;

        var driver = new TrainDriver(_layer, (_, _) => { })
        {
            Line = "U1",
            MinimumBoardingTimeInSeconds = minimalBoardingTime
        };

        var currentBoardingTime = -1;
        for (var tick = 0; tick < 10000; tick++, _layer.Context.UpdateStep())
        {
            driver.Tick();

            if (driver.Boarding)
            {
                currentBoardingTime++;
            }
            else if (currentBoardingTime > 0)
            {
                Assert.InRange(currentBoardingTime, minimalBoardingTime, TimeSpan.FromMinutes(10).TotalSeconds);
                currentBoardingTime = -1;
            }
        }
    }
}