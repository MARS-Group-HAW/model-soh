using System;
using System.Collections.Generic;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.Commons;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests.SpeedLimitTests
{
    [Collection("SimulationTests")]
    public class DecreasingSpeedLimitTests
    {

        [Fact]
        public void DecelerateFrom25To10Test()
        {
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddEntity<Bicycle>();

            var start = DateTime.Parse("2020-01-01T00:00:00");
            var config = new SimulationConfig
            {
                Globals =
                {
                    StartPoint = start,
                    EndPoint = start + TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(20),
                    DeltaTUnit = TimeSpanUnit.Seconds,
                    OutputTarget = OutputTargetType.Csv,
                    CsvOptions = {OutputPath = GetType().Name}
                },
                LayerMappings =
                {
                    new LayerMapping
                    {
                        Name = nameof(CarLayer),
                        File = Path.Combine(ResourcesConstants.NetworkFolder, "VeddelerDamm25To10.graphml")
                    },
                    new LayerMapping
                    {
                        Name = nameof(StaticTrafficLightLayer),
                        File = Path.Combine(ResourcesConstants.TrafficLightsFolder, "VeddelerDammGreenLight.csv")
                    }
                },
                AgentMappings = new List<AgentMapping>
                {
                    new AgentMapping
                    {
                        Name = nameof(Cyclist),
                        InstanceCount = 1,
                        IndividualMapping = new List<IndividualMapping>
                        {
                            new IndividualMapping {Name = "velocity", Value = 6.944},
                            new IndividualMapping {Name = "startLat", Value = 53.527625},
                            new IndividualMapping {Name = "startLon", Value = 9.981279},
                            new IndividualMapping {Name = "driveMode", Value = 6},
                            new IndividualMapping {Name = "osmRoute", Value = "[1;2]"},
                            new IndividualMapping {Name = "maxAcceleration", Value = 3},
                            new IndividualMapping {Name = "maxDeceleration", Value = 3},
                            new IndividualMapping {Name = "maxSpeed", Value = 11.11},
                            new IndividualMapping {Name = "mass", Value = 60.25},
                            new IndividualMapping {Name = "weightLoad", Value = 0},
                            new IndividualMapping {Name = "bicycleType", Value = "City"},
                            new IndividualMapping {Name = "driverType", Value = "Normal"},
                            new IndividualMapping {Name = "isEBike", Value = "false"}
                        }
                    }
                },
                EntityMappings = new List<EntityMapping>
                {
                    new EntityMapping
                    {
                        Name = nameof(Bicycle),
                        File = ResourcesConstants.BicycleCsv
                    }
                }
            };
            var starter = SimulationStarter.Start(modelDescription, config);
            var workflowState = starter.Run();

            Assert.Equal(200, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv(Path.Combine(GetType().Name, nameof(Cyclist) + ".csv"), ",");
            Assert.NotNull(table);

            var res = table.Select("Tick = '1'");
            Assert.Single(res);

            //make sure bicycle doesn't drive faster than allowed 
            res = table.Select("Tick = '90' AND CurrentEdgeId = '1'");
            Assert.Single(res);
            var currentVelocity = double.Parse((string) res[0]["Velocity"]);
            Assert.InRange(currentVelocity, 6.75, 6.94);

            //first step after crossing the intersection when max speed has decreased to 30 km/h
            var rows = table.Select("CurrentEdgeId = '2' AND Velocity > '6.75'");
            Assert.Single(rows);
            Assert.Equal("8.33", rows[0]["BicycleMaxSpeed"]);

            //make sure car doesn't drive faster than allowed but also close to max speed of 8.33 m/s
            res = table.Select("Tick = '200' AND CurrentEdgeId = '2'");
            Assert.Single(res);
            currentVelocity = double.Parse((string) res[0]["Velocity"]);
            Assert.True(currentVelocity <= 2.78);
            Assert.True(currentVelocity > 2.6);
        }
    }
}