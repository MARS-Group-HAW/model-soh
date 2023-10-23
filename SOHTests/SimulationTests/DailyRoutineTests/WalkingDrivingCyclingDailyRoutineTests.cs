using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHBicycleModel.Rental;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHDomain.Graph;
using SOHMultimodalModel.Layers;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHMultimodalModel.Model;
using Xunit;

namespace SOHTests.SimulationTests.DailyRoutineTests;

[Collection("SimulationTests")]
public class WalkingDrivingCyclingDailyRoutineTests
{
    [Fact]
    [Trait("Category", "External")]
    public void SimulateOneDay()
    {
        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });

        description.AddLayer<BicycleRentalLayer>(new[] { typeof(IBicycleRentalLayer) });
        description.AddLayer<CarParkingLayer>(new[] { typeof(ICarParkingLayer) });

        description.AddLayer<VectorBuildingsLayer>();
        description.AddLayer<VectorLanduseLayer>();
        description.AddLayer<VectorPoiLayer>();
        description.AddLayer<MediatorLayer>();


        description.AddLayer<CitizenLayer>();
        description.AddLayer<CarLayer>();
        description.AddLayer<TrafficLightLayer>();
        description.AddAgent<Citizen, CitizenLayer>();
        description.AddEntity<Car>();
        description.AddEntity<RentalBicycle>();

        var startPoint = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Execution =
            {
                MaximalLocalProcess = 1
            },
            Globals =
            {
                StartPoint = startPoint,
                EndPoint = startPoint + TimeSpan.FromHours(24),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.None,
                SqLiteOptions =
                {
                    DatabaseName = "my_results"
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    File = ResourcesConstants.DriveGraphAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(CarParkingLayer),
                    File = ResourcesConstants.ParkingAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(BicycleRentalLayer),
                    File = ResourcesConstants.BicycleRentalAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(TrafficLightLayer),
                    File = ResourcesConstants.TrafficLightsAltona
                },
                new LayerMapping
                {
                    Name = nameof(VectorBuildingsLayer),
                    File = ResourcesConstants.BuildingsAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(VectorLanduseLayer),
                    File = ResourcesConstants.LanduseAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(VectorPoiLayer),
                    File = ResourcesConstants.PoisAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(SpatialGraphMediatorLayer),
                    Inputs = new List<Input>
                    {
                        new()
                        {
                            File = ResourcesConstants.WalkGraphAltonaAltstadt,
                            InputConfiguration = new InputConfiguration
                                { Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking } }
                        },
                        new()
                        {
                            File = ResourcesConstants.DriveGraphAltonaAltstadt,
                            InputConfiguration = new InputConfiguration
                            {
                                Modalities = new HashSet<SpatialModalityType>
                                    { SpatialModalityType.Cycling, SpatialModalityType.CarDriving }
                            }
                        }
                    }
                }
            },
            EntityMappings =
            {
                new EntityMapping
                {
                    Name = nameof(Car),
                    File = ResourcesConstants.CarCsv
                },
                new EntityMapping
                {
                    Name = nameof(RentalBicycle),
                    File = ResourcesConstants.BicycleCsv
                }
            },
            AgentMappings =
            {
                new AgentMapping
                {
                    Name = nameof(Citizen),
                    InstanceCount = 1,
                    File = Path.Combine("res", "agent_inits", "CitizenInit10k.csv"),
                    IndividualMapping =
                    {
                        new IndividualMapping { Name = "CapabilityDriving", Value = true },
                        new IndividualMapping { Name = "CapabilityCycling", Value = true }
                    },
                    OutputFilter =
                    {
                        new OutputFilter
                        {
                            Name = "StoreTickResult",
                            Values = new object[] { true },
                            Operator = ContainsOperator.In
                        }
                    }
                }
            }
        };

        LoggerFactory.SetLogLevel(LogLevel.Off);
        var application = SimulationStarter.BuildApplication(description, config);
        var simulation = application.Resolve<ISimulation>();
        simulation.StartSimulation();

        // var modelAllActiveLayers = state.Model.AllActiveLayers;
        // foreach (var layer in modelAllActiveLayers)
        // {
        //     if (!(layer is CitizenLayer citizenLayer)) continue;
        //     var pedestrianMapValues = citizenLayer.PedestrianMap.Values;
        //     TripsOutputAdapter.PrintTripResult(new List<MultimodalAgent>(pedestrianMapValues));
        // }
    }
}