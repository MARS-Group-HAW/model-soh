using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHCarModel.Model;
using SOHTests.Commons;
using SOHTests.Commons.Environment;
using SOHTests.SimulationTests.CarDrivingTests.IntersectionBehaviorTests;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.CarDrivingTests.BasicDrivingTests;

[Collection("SimulationTests")]
public class AccelerationTests : IClassFixture<SpatialGraphFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;

    public AccelerationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void AccelerateFrom0To50Test()
    {
        _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
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
                EndPoint = start + TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(30),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
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
        var workflowState = starter.Run();

        Assert.Equal(210, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"), ',');
        Assert.NotNull(table);

        var res = table.Select("Velocity = '13.87'");
        Assert.Equal("39", res[0]["Step"]);
    }
}