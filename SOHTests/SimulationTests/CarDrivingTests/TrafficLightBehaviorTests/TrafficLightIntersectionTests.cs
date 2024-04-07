using System;
using System.Collections.Generic;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHModel.Multimodal.Layers.TrafficLight;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.TrafficLightBehaviorTests;

[Collection("SimulationTests")]
public class TrafficLightIntersectionTests
{
    [Fact]
    public void ThreeCarsIntersectionWithLightsTest()
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
            Globals =
            {
                StartPoint = start,
                EndPoint = start + TimeSpan.FromMinutes(2),
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
                    File = Path.Combine(ResourcesConstants.NetworkFolder, "GertigstrasseMuehlenkamp.graphml")
                },
                new LayerMapping
                {
                    Name = nameof(TrafficLightLayer),
                    File = Path.Combine(ResourcesConstants.TrafficLightsFolder, "traffic_lights_winterhude.zip")
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 3,
                    File = Path.Combine(ResourcesConstants.AgentInitsFolder, "TrafficLightIntersectionCars.csv")
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
        Assert.Equal(120, workflowState.Iterations);

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        //make sure that the cars reach the goal
        var car1ReachesGoal =
            table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001' AND GoalReached = 'True'");
        var car2ReachesGoal =
            table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002' AND GoalReached = 'True'");
        var car3ReachesGoal =
            table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003' AND GoalReached = 'True'");
        Assert.NotEmpty(car1ReachesGoal);
        Assert.NotEmpty(car2ReachesGoal);
        Assert.NotEmpty(car3ReachesGoal);
    }
}