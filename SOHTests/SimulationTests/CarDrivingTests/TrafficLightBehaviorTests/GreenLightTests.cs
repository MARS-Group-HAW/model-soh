using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core;
using Mars.Common.Core.Logging;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHTests.Commons;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.TrafficLightBehaviorTests;

[Collection("SimulationTests")]
public class GreenLightTests
{
    [Fact]
    public void CarAgentSeesGreenLightAndDoesNotBreakTest()
    {
        LoggerFactory.SetLogLevel(LogLevel.Warning);

        var modelDescription = new ModelDescription();

        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddLayer<StaticTrafficLightLayer>();
        modelDescription.AddEntity<Car>();

        var start = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromMinutes(5),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions =
                {
                    Delimiter = ",",
                    NumberFormat = "F2",
                    OutputPath = GetType().Name
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = VeddelerDammGraphEnv.CreateInstance()
                },
                new LayerMapping
                {
                    Name = nameof(StaticTrafficLightLayer),
                    File = ResourcesConstants.TrafficLightGreenVeddelerDamm
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 1,
                    IndividualMapping = new List<IndividualMapping>
                    {
                        new() { Name = "startLat", Value = 53.527625 },
                        new() { Name = "startLon", Value = 9.981279 },
                        new() { Name = "driveMode", Value = 6 },
                        new() { Name = "osmRoute", Value = "[1;2]" }
                    }
                }
            },
            EntityMappings = new List<EntityMapping>
            {
                new()
                {
                    Name = nameof(Car),
                    File = ResourcesConstants.CarCsv
                }
            }
        };
        var starter = SimulationStarter.Start(modelDescription, config);
        starter.Run();

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        //make sure that the car has detected the traffic light and thinks that it is green
        var colorRes = table.Select("CurrentEdgeId = '1' AND" +
                                    " Convert(RemainingDistanceOnEdge, 'System.Decimal') < '100.0' AND" +
                                    " Convert(RemainingDistanceOnEdge, 'System.Decimal') > '0'");
        foreach (var dataRow in colorRes)
            Assert.Equal(TrafficLightPhase.Green.ToString(), dataRow["NextTrafficLightPhase"]);

        //check if the car changes its speed after accelerating to max speed (this shouldn't happen)
        var speedRes = table.Select("Convert(Tick, 'System.Int32') > 50 AND " +
                                    "Convert(Tick, 'System.Int32') < 150");

        foreach (var dataRow in speedRes)
        {
            var velocity = dataRow["Velocity"].Value<double>();
            Assert.True(13.85 < velocity);
            Assert.True(13.95 > velocity);
        }
    }
}