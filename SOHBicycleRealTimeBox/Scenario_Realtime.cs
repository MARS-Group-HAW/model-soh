using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core;
using Mars.Interfaces.Model;
using SOHModel.Bicycle.Rental;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Routing;

namespace SOHBicycleRealTime;

public static class ScenarioRealtime
{
    public static SimulationConfig Get()
    {
        // var dateTime = DateTime.Now;
        var start = "2020-12-14T16:00:00".Value<DateTime>();
        var end = "2020-12-14T20:00:00".Value<DateTime>();

        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = start,
                EndPoint = end,
                DeltaTUnit = TimeSpanUnit.Seconds,
                ShowConsoleProgress = true
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(SidewalkLayer),
                    File = Path.Combine("resources", "harburg_walk_graph.geojson")
                },
                new LayerMapping
                {
                    Name = nameof(BicycleRentalLayer),
                    File = Path.Combine("resources", "bicycle_rental_layer_complete.geojson"),
                    InputConfiguration = new InputConfiguration
                    {
                        ValidTimeAtAttributeName = "time",
                        TemporalJoinReference = "thingId"
                    },
                    OutputTarget = OutputTargetType.Csv,
                    OutputFrequency = GlobalConfig.OutputFrequency,
                    IndividualMapping = new List<IndividualMapping>
                    {
                        new()
                        {
                            Name = "synchronizations", Value = new[]
                            {
                                "2020-12-14T17:00:00".Value<DateTime>(),
                                "2020-12-14T18:00:00".Value<DateTime>(),
                                "2020-12-14T19:00:00".Value<DateTime>()
                            }
                        }
                    }
                },
                new LayerMapping
                {
                    Name = nameof(CycleTravelerSchedulerLayer),
                    File = Path.Combine("resources", "cycle_traveler.csv")
                },
                new LayerMapping
                {
                    Name = nameof(GatewayLayer),
                    File = Path.Combine("resources", "hamburg_sbahn_stations.geojson")
                }
            },
            EntityMappings =
            {
                new EntityMapping
                {
                    Name = nameof(RentalBicycle),
                    File = Path.Combine("resources", "bicycle.csv")
                }
            },
            AgentMappings =
            {
                new AgentMapping
                {
                    Name = nameof(CycleTraveler),
                    InstanceCount = 0,
                    OutputFilter =
                    {
                        new OutputFilter
                        {
                            Name = "StoreTickResult",
                            Values = new object[] { true },
                            Operator = ContainsOperator.In
                        }
                    },
                    IndividualMapping =
                    {
                        new IndividualMapping { Name = "ResultTrajectoryEnabled", Value = true },
                        new IndividualMapping { Name = "CapabilityDriving", Value = true },
                        new IndividualMapping { Name = "CapabilityCycling", Value = true }
                    }
                }
            }
        };

        return config;
    }
}