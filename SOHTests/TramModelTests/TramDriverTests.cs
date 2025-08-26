using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Tram.Model;
using SOHModel.Tram.Route;
using SOHModel.Tram.Station;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.TramModelTests
{
    /// <summary>
    /// High-confidence functional tests for Straßenbahn (Tram) running on TrainDriving modality.
    /// This class mirrors TrainDriverTests semantics and extends with extra consistency checks.
    /// </summary>
    public class TramDriverTests : IClassFixture<TramRouteLayerFixture>
    {
        private readonly TramLayer _layer;
        private readonly TramRouteLayerFixture _routeLayerFixture;

        public TramDriverTests(TramRouteLayerFixture routeLayerFixture)
        {
            _routeLayerFixture = routeLayerFixture;
            _layer = new TramLayer(routeLayerFixture.TramRouteLayer)
            {
                Context = SimulationContext.Start2020InSeconds,
                EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.TramCsv)),
                GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
                {
                    GraphImports = new List<Input>
                    {
                        new()
                        {
                            File = ResourcesConstants.TramT1Graph,
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
        public void SufficientBoardingTimeAtStations()
        {
            const int minimalBoardingTime = 32;

            var driver = new TramDriver(_layer, (_, _) => { })
            {
                Line = "T1",
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

            var driver = new TramDriver(_layer, (_, _) => { })
            {
                Line = "T1",
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

                    var driverStationStops = driver.CurrentTramRouteEntry;
                    var travelTime = driverStationStops.Minutes * 60;
                    nextStartTick += travelTime;
                }
            }
        }

        [Fact]
        public void TramReachesStationsWithinDefinedStopMinutes()
        {
            var driver = new TramDriver(_layer, (_, _) => { })
            {
                Line = "T1",
                MinimumBoardingTimeInSeconds = 20
            };

            long firstDrivingTick = -1;
            bool waitingForArrivalAfterDeparture = false;
            int scheduledSecondsForCurrentLeg = 0;

            const int ticks = 20000;
            for (var tick = 0; tick < ticks; tick++, _layer.Context.UpdateStep())
            {
                var wasBoarding = driver.Boarding;
                driver.Tick();

                // Departure transition: Boarding -> Driving
                if (wasBoarding && !driver.Boarding)
                {
                    firstDrivingTick = tick;
                    // The schedule for the *current* leg (in seconds)
                    scheduledSecondsForCurrentLeg = driver.CurrentTramRouteEntry.Minutes * 60;
                    waitingForArrivalAfterDeparture = true;
                    continue;
                }

                // Arrival transition: Driving -> Boarding (first tick we begin dwelling)
                if (!wasBoarding && driver.Boarding && waitingForArrivalAfterDeparture)
                {
                    waitingForArrivalAfterDeparture = false;

                    var actualDrivingSeconds = tick - firstDrivingTick;

                    // ✅ Upper bound: driving must NOT exceed the scheduled segment time.
                    // (It can be shorter; the remainder is dwell until the scheduled next departure.)
                    Assert.InRange(actualDrivingSeconds, 1, scheduledSecondsForCurrentLeg);

                    // Reset for potential subsequent legs (optional; variables will be overwritten at next departure)
                    firstDrivingTick = -1;
                    scheduledSecondsForCurrentLeg = 0;
                }
            }

            Assert.False(waitingForArrivalAfterDeparture);
        }


        [Fact]
        public void ImportTramwayTrack()
        {
            var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.TramT1Graph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true
                        }
                    }
                }
            });
            Assert.Equal(11, environment.Nodes.Count);
            Assert.Equal(20, environment.Edges.Count);

            // all nodes are connected to the graph
            Assert.All(environment.Nodes, node => Assert.NotEmpty(node.OutgoingEdges));
        }

        [Fact]
        public void TestMoveTramAlongBidirectionalPath()
        {
            var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.TramT1Graph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true
                        }
                    }
                }
            });

            var manager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.TramCsv));

            var layer = new TramLayer(_routeLayerFixture.TramRouteLayer)
            {
                EntityManager = manager,
                GraphEnvironment = environment,
                Context = SimulationContext.Start2020InSeconds
            };

            var p1 = environment.FindShortestRoute(environment.Nodes.Last(), environment.Nodes.First());
            var p2 = environment.FindShortestRoute(environment.Nodes.First(), environment.Nodes.Last());
            Assert.Equal(p1.Count, p2.Count);
            Assert.Equal(p1.RemainingRouteDistanceToGoal, p2.RemainingRouteDistanceToGoal, 6);
            var source = environment.Nodes.First().Position;
            var target = environment.Nodes.Last().Position;

            var route = environment.FindShortestRoute(environment.NearestNode(source),
                environment.NearestNode(target));

            Assert.NotEmpty(route);
            var goalReached = false;

            var driver = new TramDriver(layer, (_, _) => goalReached = true)
            {
                Line = "U1",
                Position = source,
                TramRoute = new TramRoute
                {
                    Entries = route.Select(stop => new TramRouteEntry(
                        new TramStation
                        {
                            Position = stop.Edge.From.Position
                        }, new TramStation
                        {
                            Position = stop.Edge.To.Position
                        }, 0)).ToList()
                }
            };

            Assert.NotNull(driver.Tram);
            Assert.NotNull(driver.Layer);
            Assert.NotEqual(Guid.Empty, driver.ID);
            for (var i = 0; i < 10000; i++, layer.Context.UpdateStep()) driver.Tick();
            Assert.True(goalReached);
            Assert.True(driver.StationStops > 0);
            Assert.True(driver.GoalReached);
            Assert.NotEqual(source, driver.Position);
        }

        [Fact]
        public void MoveAlongReveredTramRouteTest()
        {
            var driver = new TramDriver(_layer, (_, _) => { })
            {
                Line = "T1",
                MinimumBoardingTimeInSeconds = 10,
                ReversedRoute = true
            };

            driver.Tick();

            var startTramRouteEntry = driver.CurrentTramRouteEntry;
            Assert.NotNull(startTramRouteEntry);
            Assert.Equal("Facultes", startTramRouteEntry.From.Name);

            for (var tick = 0; tick < 10000; tick++, _layer.Context.UpdateStep()) driver.Tick();

            var goalTramRouteEntry = driver.CurrentTramRouteEntry;
            Assert.NotNull(goalTramRouteEntry);
            Assert.Equal("Panoramique", goalTramRouteEntry.To.Name);
        }
    }
}