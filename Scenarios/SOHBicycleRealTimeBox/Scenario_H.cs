using System;
using System.IO;
using Mars.Common.Core;
using Mars.Interfaces.Model;
using SOHBicycleModel.Rental;
using SOHDomain.Graph;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Routing;

namespace SOHBicycleRealTime;

public static class ScenarioH
{
    public static SimulationConfig Get()
    {
        // var dateTime = DateTime.Now;
        var start = "2020-12-14T08:00:00".Value<DateTime>();
        var end = "2020-12-14T20:00:00".Value<DateTime>();

        var config = new SimulationConfig
        {
            SimulationIdentifier = "H",
            Globals =
            {
                StartPoint = start,
                EndPoint = end,
                DeltaTUnit = TimeSpanUnit.Seconds,
                ShowConsoleProgress = true,
                GeoJsonOptions = { OutputPath = "results" },
                PostgresSqlOptions =
                {
                    HostUserName = "mars", HostPassword = "sram2020", DatabaseName = "mars",
                    OverrideByConflict = true
                }
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
                    OutputTarget = OutputTargetType.PostgresSql,
                    OutputFrequency = GlobalConfig.OutputFrequency
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