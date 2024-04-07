using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHTests.Commons;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.SpeedLimitTests;

[Collection("SimulationTests")]
public class DecreasingSpeedLimitTests
{
    [Fact]
    public void DecelerateFrom50To30Test()
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
                EndPoint = start + TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(20),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = VeddelerDammGraphEnv.CreateInstance(1150, 10000, 50, 30)
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

        Assert.Equal(200, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        //first tick where max speed should be 50 km/h
        var res = table.Select("Tick = '1'");
        Assert.Single(res);
        Assert.Equal(13.89, res[0]["SpeedLimit"].Value<double>(), 2);

        //make sure car doesn't drive faster than allowed but also close to max speed of 13.89 m/s
        res = table.Select("Tick = '90' AND CurrentEdgeId = '1'");
        Assert.Single(res);
        var currentVelocity = res[0]["Velocity"].Value<double>();
        Assert.True(currentVelocity <= 13.89);
        Assert.True(currentVelocity > 13.85);

        //first step after crossing the intersection when max speed has decreased to 30 km/h
        var rows = table.Select("CurrentEdgeId = '2' AND Velocity = '13.87'");
        Assert.Single(rows);
        Assert.Equal(8.33, rows[0]["SpeedLimit"].Value<double>(), 2);

        //make sure car doesn't drive faster than allowed but also close to max speed of 8.33 m/s
        res = table.Select("Tick = '200' AND CurrentEdgeId = '2'");
        Assert.Single(res);
        currentVelocity = res[0]["Velocity"].Value<double>();
        Assert.True(currentVelocity <= 8.34);
        Assert.True(currentVelocity > 8.25);
    }
}