using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHCarModel.Model;
using SOHDomain.Common;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.TurningTests;

[Collection("SimulationTests")]
public class TurningTests
{
    [Fact]
    public void SlowDownBeforeLeftTurnTest()
    {
        var modelDescription = new ModelDescription();
        modelDescription.AddLayer<CarLayer>();
        modelDescription.AddAgent<CarDriver, CarLayer>();
        modelDescription.AddEntity<Car>();

        var start = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(40),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions =
                {
                    Delimiter = ",",
                    NumberFormat = "G",
                    OutputPath = GetType().Name
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    File = Path.Combine(ResourcesConstants.NetworkFolder, "square.graphml")
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
                        new() { Name = "startLat", Value = 53.5819 },
                        new() { Name = "startLon", Value = 10.01121 },
                        new() { Name = "driveMode", Value = 6 },
                        new() { Name = "osmRoute", Value = "[1;2;3;4]" }
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

        Assert.Equal(100, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        //check that speed is below allowed turning speed for turn 1
        var restTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
                                     "Convert(CurrentEdgeId, 'System.Int32') = 1 AND " +
                                     "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 4");

        double currentVelocity;
        foreach (var row in restTurn1)
        {
            currentVelocity = row["Velocity"].Value<double>();
            Assert.True(currentVelocity < VehicleConstants.RegularTurnSpeed + 0.1);
        }

        //check that the car accelerates after the first turning maneuver
        var speedUpTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND  " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
                                        "Convert(PositionOnEdge, 'System.Decimal') > 5");

        currentVelocity = speedUpTurn1[0]["Velocity"].Value<double>();
        Assert.True(currentVelocity > VehicleConstants.IntersectionSpeed + 0.01);

        //check that speed is below allowed turning speed for turn 2
        var restTurn2 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
                                     "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
                                     "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 4");
        currentVelocity = restTurn2[0]["Velocity"].Value<double>();
        Assert.True(currentVelocity < VehicleConstants.RegularTurnSpeed + 0.1);
    }
}