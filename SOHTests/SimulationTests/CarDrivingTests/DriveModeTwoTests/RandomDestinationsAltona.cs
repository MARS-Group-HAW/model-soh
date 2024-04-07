using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core;
using Mars.Common.Core.Logging;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Components.Starter;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHModel.Multimodal.Layers.TrafficLight;
using SOHTests.SimulationTests.CarDrivingTests.IntersectionBehaviorTests;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.DriveModeTwoTests;

[Collection("SimulationTests")]
public class RandomDestinationsAltona : IClassFixture<SpatialGraphFixture>
{
    private readonly SpatialGraphFixture _fixture;

    public RandomDestinationsAltona(SpatialGraphFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void RandomDestinationsAltonaTest()
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var modelDescription = new ModelDescription();
        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddLayer<TrafficLightLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddEntity<Car>();

        var startTime = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = startTime,
                EndPoint = startTime + TimeSpan.FromMinutes(10),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions =
                {
                    NumberFormat = "G",
                    Delimiter = ";",
                    OutputPath = GetType().Name
                }
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
                    InstanceCount = 10,
                    IndividualMapping = new List<IndividualMapping>
                    {
                        new() { Name = "driveMode", Value = 2 }
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

        //check that all agents have moved
        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        var positionById = new Dictionary<string, Position>();
        var firstTickRows = table.Select("Tick = '0'");
        foreach (var row in firstTickRows)
        {
            var id = row["ID"].Value<string>();
            var position =
                Position.CreateGeoPosition(row["Longitude"].Value<double>(), row["Latitude"].Value<double>());
            positionById.Add(id, position);
        }

        Assert.Equal(10, positionById.Count);

        var lastTickRows = table.Select("Tick = '600'");
        foreach (var row in lastTickRows)
        {
            var id = row["ID"].Value<string>();
            var position =
                Position.CreateGeoPosition(row["Longitude"].Value<double>(), row["Latitude"].Value<double>());
            Assert.NotEqual(positionById[id], position);
        }
    }
}