using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHBusModel.Model;
using SOHBusModel.Route;
using SOHBusModel.Station;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.BusModelTests;

public class BusDriverTests : IClassFixture<BusRouteLayerFixture>
{
    private readonly BusLayer _layer;
    private readonly BusRouteLayerFixture _routeLayerFixture;

    public BusDriverTests(BusRouteLayerFixture routeLayerFixture)
    {
        _routeLayerFixture = routeLayerFixture;
        _layer = new BusLayer(routeLayerFixture.BusRouteLayer)
        {
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.BusCsv)),
            GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.Bus113Graph,
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
    public void VisitAllStationsAlongBusRoute()
    {
        const string line = "113";
        var driver = new BusDriver(_layer, (_, _) => { })
        {
            Line = line,
            MinimumBoardingTimeInSeconds = 10
        };

        Assert.True(_layer.BusRouteLayer.TryGetRoute(line, out var schedule));
        var unvisitedStationEntries = schedule.Entries.ToList();
        Assert.Equal(7, unvisitedStationEntries.Count);
        for (var tick = 0; tick < 9000; tick++, _layer.Context.UpdateStep())
        {
            driver.Tick();
            if (driver.Boarding)
            {
                var routeEntry = driver.BusRouteEnumerator.Current;
                unvisitedStationEntries.Remove(routeEntry);
            }
        }

        Assert.Empty(unvisitedStationEntries);
    }

    [Fact]
    public void SufficientBoardingTimeAtStations()
    {
        const int minimalBoardingTime = 32;

        var driver = new BusDriver(_layer, (_, _) => { })
        {
            Line = "113",
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

    [Fact]
    public void PunctualStartingTimeForNextRoute()
    {
        const int minimumBoardingTimeInSeconds = 10;

        var driver = new BusDriver(_layer, (_, _) => { })
        {
            Line = "113",
            MinimumBoardingTimeInSeconds = minimumBoardingTimeInSeconds
        };

        var firstTickAfterBoarding = false;

        var nextStartTick = minimumBoardingTimeInSeconds + 1;
        for (var tick = 0; tick < 2000; tick++, _layer.Context.UpdateStep())
        {
            driver.Tick();

            if (driver.Boarding)
            {
                firstTickAfterBoarding = true;
            }
            else if (firstTickAfterBoarding)
            {
                firstTickAfterBoarding = false;

                Assert.InRange(Math.Abs(tick - nextStartTick), 0, minimumBoardingTimeInSeconds * 2);

                var driverStationStops = driver.CurrentBusRouteEntry;
                var travelTime = driverStationStops.Minutes * 60;
                nextStartTick += travelTime;
            }
        }
    }

    [Fact]
    public void BusReachesReachStationsWithinDefinedStopMinutes()
    {
        var driver = new BusDriver(_layer, (_, _) => { })
        {
            Line = "113",
            MinimumBoardingTimeInSeconds = 20
        };


        var firstDrivingTick = -1L;
        var isFirstBoardingTick = true;
        var travelDurance = 0;

        const int ticks = 2000;
        for (var tick = 0; tick < ticks; tick++, _layer.Context.UpdateStep())
        {
            driver.Tick();

            if (driver.Boarding)
            {
                if (isFirstBoardingTick && firstDrivingTick >= 0)
                    Assert.InRange(tick, +travelDurance, ticks);

                firstDrivingTick = -1;
            }
            else if (firstDrivingTick < 0)
            {
                firstDrivingTick = tick;
                travelDurance = driver.CurrentBusRouteEntry.Minutes * 60 -
                                driver.MinimumBoardingTimeInSeconds * 2;
            }

            isFirstBoardingTick = !driver.Boarding;
        }

        var goal = driver.BusRoute.Entries.Last().To.Position;
        Assert.Equal(goal, driver.Position);
    }

    [Fact]
    public void Import113Track()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.Bus113Graph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true
                    }
                }
            }
        });
        Assert.Equal(8, environment.Nodes.Count);
        Assert.Equal(21, environment.Edges.Count);

        // all nodes are connected to the graph
        Assert.All(environment.Nodes, node => Assert.NotEmpty(node.OutgoingEdges));
    }

    [Fact]
    public void TestMoveBusAlongBidirectionalPath()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.Bus113Graph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true
                    }
                }
            }
        });

        var manager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.BusCsv));

        var layer = new BusLayer(_routeLayerFixture.BusRouteLayer)
        {
            EntityManager = manager,
            GraphEnvironment = environment,
            Context = SimulationContext.Start2020InSeconds
        };

        var p1 = environment.FindShortestRoute(environment.Nodes.Last(), environment.Nodes.First());
        var p2 = environment.FindShortestRoute(environment.Nodes.First(), environment.Nodes.Last());
        Assert.Equal(p1.Count, p2.Count);
        var source = environment.Nodes.First().Position;
        var target = environment.Nodes.Last().Position;

        var route = environment.FindShortestRoute(environment.NearestNode(source),
            environment.NearestNode(target));

        Assert.NotEmpty(route);
        var goalReached = false;

        var driver = new BusDriver(layer, (_, _) => goalReached = true)
        {
            Line = "113",
            Position = source,
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

        Assert.NotNull(driver.Bus);
        Assert.NotNull(driver.Layer);
        Assert.NotEqual(Guid.Empty, driver.ID);
        for (var i = 0; i < 10000; i++, layer.Context.UpdateStep()) driver.Tick();
        Assert.True(goalReached);
        Assert.True(driver.StationStops > 0);
        Assert.True(driver.GoalReached);
        Assert.NotEqual(source, driver.Position);
    }
}