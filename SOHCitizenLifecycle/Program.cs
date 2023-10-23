using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mars.Common.Core;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHBicycleModel.Rental;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHDomain.Output;
using SOHMultimodalModel.Layers;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Output.Trips;

namespace SOHCitizenLifecycle
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            LoggerFactory.SetLogLevel(LogLevel.Off);

            var description = new ModelDescription();
            description.AddLayer<CarParkingLayer>();
            description.AddLayer<CarLayer>();
            description.AddLayer<CitizenSchedulerLayer>();
            description.AddLayer<BicycleRentalLayer>();
            description.AddLayer<VectorBuildingsLayer>();
            description.AddLayer<VectorLanduseLayer>();
            description.AddLayer<VectorPoiLayer>();
            description.AddLayer<MediatorLayer>();
            description.AddLayer<CitizenLayer>();
            description.AddLayer<TrafficLightLayer>();
            description.AddAgent<Citizen, CitizenLayer>();

            ISimulationContainer application;
            if (args != null && args.Any())
            {
                application = SimulationStarter.BuildApplication(description, args);
            }
            else
            {
                var config = GetConfig();
                application = SimulationStarter.BuildApplication(description, config);
            }

            var simulation = application.Resolve<ISimulation>();

            var watch = Stopwatch.StartNew();
            var state = simulation.StartSimulation();
            watch.Stop();


            var modelAllActiveLayers = state.Model.Layers;
            var agents = new List<ITripSavingAgent>();
            foreach (var pair in modelAllActiveLayers)
            {
                var layer = pair.Value;

                if (layer is CitizenLayer citizenLayer)
                {
                    var tm =
                        description.SimulationConfig.TypeMappings.FirstOrDefault(m => m.Name == "Citizen");
                    if (tm != null)
                        if (tm.ParameterMapping.TryGetValue("ResultTrajectoryEnabled", out var p) &&
                            p.Value != null && p.Value.Value<bool>())
                            agents.AddRange(citizenLayer.PedestrianMap.Values);
                }
            }

            TripsOutputAdapter.PrintTripResult(agents);

            Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        }

        private static SimulationConfig GetConfig()
        {
            SimulationConfig simulationConfig;
            var configValue = Environment.GetEnvironmentVariable("CONFIG");

            if (configValue != null)
            {
                Console.WriteLine("Use passed simulation config by environment variable");
                simulationConfig = SimulationConfig.Deserialize(configValue);
                Console.WriteLine(simulationConfig.Serialize());
            }

            var startPoint = DateTime.Parse("2020-01-01T00:00:00");

            var config = new SimulationConfig
            {
                Globals =
                {
                    StartPoint = startPoint,
                    EndPoint = startPoint + TimeSpan.FromHours(12),
                    DeltaTUnit = TimeSpanUnit.Seconds,
                    ShowConsoleProgress = true,
                    OutputTarget = OutputTargetType.SqLite
                },
                LayerMappings =
                {
                    new LayerMapping
                        {Name = nameof(CitizenSchedulerLayer), File = Path.Combine("resources", "citizen.csv")},
                    new LayerMapping
                    {
                        Name = nameof(TrafficLightLayer), File = Path.Combine("resources", "altona_traffic_lights.zip")
                    },
                    new LayerMapping
                    {
                        Name = nameof(VectorBuildingsLayer),
                        File = Path.Combine("resources", "altona_buildings.geojson")
                    },
                    new LayerMapping
                        {Name = nameof(VectorLanduseLayer), File = Path.Combine("resources", "altona_landuse.geojson")},
                    new LayerMapping
                        {Name = nameof(VectorPoiLayer), File = Path.Combine("resources", "altona_pois.geojson")},
                    new LayerMapping
                        {Name = nameof(CarLayer), File = Path.Combine("resources", "altona_drive_graph.geojson")},
                    new LayerMapping
                    {
                        Name = nameof(CarParkingLayer),
                        File = Path.Combine("resources", "altona_parking_spaces.geojson")
                    },
                    new LayerMapping
                    {
                        Name = nameof(BicycleRentalLayer),
                        File = Path.Combine("resources", "altona_bicycle_rental_stations.geojson")
                    },
                    new LayerMapping
                    {
                        Name = nameof(CitizenLayer), File = Path.Combine("resources", "altona_walk_graph.geojson"),
                        IndividualMapping = {new IndividualMapping {Name = "ParkingOccupancy", Value = 0.779}}
                    }
                },
                AgentMappings =
                {
                    new AgentMapping
                    {
                        Name = nameof(Citizen),
                        // InstanceCount = 100,
                        // File = Path.Combine("resources", "citizen_init.csv"),
                        OutputTarget = OutputTargetType.SqLite,


                        OutputFilter =
                        {
                            new OutputFilter
                            {
                                Name = "StoreTickResult", Operator = ContainsOperator.In, Values = new object[] {true}
                            }
                        },

                        IndividualMapping =
                        {
                            new IndividualMapping {Name = "ResultTrajectoryEnabled", Value = true},
                            new IndividualMapping {Name = "CapabilityDriving", Value = true},
                            new IndividualMapping {Name = "CapabilityCycling", Value = true}
                        }
                    }
                },
                EntityMappings =
                {
                    new EntityMapping {Name = nameof(Car), File = Path.Combine("resources", "car.csv")}
                }
            };

            Console.WriteLine("Use pre-defined simulation config");
            simulationConfig = config;

            Console.WriteLine("Used simulation config:");
            Console.WriteLine(simulationConfig.Serialize());

            return config;
        }
    }
}