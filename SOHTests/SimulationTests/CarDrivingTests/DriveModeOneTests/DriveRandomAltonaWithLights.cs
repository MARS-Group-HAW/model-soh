using System;
using System.Collections.Generic;
using Mars.Common.Core.Logging;
using Mars.Components.Environments;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHModel.Multimodal.Layers.TrafficLight;
using SOHTests.SimulationTests.CarDrivingTests.IntersectionBehaviorTests;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.DriveModeOneTests;

[Collection("SimulationTests")]
public class DriveRandomAltonaWithLights : IClassFixture<SpatialGraphFixture>
{
    private readonly SpatialGraphFixture _fixture;

    public DriveRandomAltonaWithLights(SpatialGraphFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void DriveRandomAltonaWithLightsTest()
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var modelDescription = new ModelDescription();
        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddLayer<TrafficLightLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddEntity<Car>();

        var start = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Execution =
            {
                MaximalLocalProcess = 1
            },
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromMinutes(10),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(_fixture.DriveGraphAltonaAltstadt)
                },
                new LayerMapping
                {
                    Name = nameof(TrafficLightLayer),
                    File = ResourcesConstants.TrafficLightsAltona
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 40,
                    IndividualMapping = new List<IndividualMapping>
                    {
                        new() { Name = "driveMode", Value = 1 }
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

        Assert.Equal(600, workflowState.Iterations);
    }
}