using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHCarModel.Model;
using SOHTests.Commons;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.BasicDrivingTests;

[Collection("SimulationTests")]
public class DecelerationTests
{
    [Fact]
    public void BrakeFrom50To0Within100MeterTest()
    {
        var modelDescription = new ModelDescription();
        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddLayer<StaticTrafficLightLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddEntity<Car>();

        var start = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromSeconds(40),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = VeddelerDammGraphEnv.CreateInstance(102)
                },
                new LayerMapping
                {
                    Name = nameof(StaticTrafficLightLayer),
                    File = ResourcesConstants.TrafficLightRedVeddelerDamm
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
                        new() { Name = "velocity", Value = 13.89 },
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
        var workflowState = starter.Run();

        Assert.Equal(40, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"), ',');
        Assert.NotNull(table);

        var res = table.Select("Convert(Tick, 'System.Int32') > '1' AND " +
                               "Convert(Velocity, 'System.Decimal')  = '0'");
        Assert.Equal("22", res[0]["Step"]);
    }

    [Fact]
    public void BrakeFrom50To0Within25MeterTest()
    {
        var modelDescription = new ModelDescription();
        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddLayer<StaticTrafficLightLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddEntity<Car>();

        var start = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromSeconds(40),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = VeddelerDammGraphEnv.CreateInstance(27)
                },
                new LayerMapping
                {
                    Name = nameof(StaticTrafficLightLayer),
                    File = ResourcesConstants.TrafficLightRedVeddelerDamm
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
                        new() { Name = "velocity", Value = 13.89 },
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
        var workflowState = starter.Run();

        Assert.Equal(40, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"), ',');
        Assert.NotNull(table);

        var res = table.Select("Convert(Tick, 'System.Int32') > '1' AND " +
                               "Convert(Velocity, 'System.Decimal')  = '0'");
        Assert.Equal("18", res[0]["Step"]);
    }
}