using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHCarModel.Model;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.IntersectionBehaviorTests;

[Collection("SimulationTests")]
public class FourWayStopTests
{
    [Fact]
    public void FourWayStopTwoCarsGiveWayTest()
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
                EndPoint = start + TimeSpan.FromMinutes(2),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    File = ResourcesConstants.DriveGraphFourWayIntersection
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 2,
                    File = Path.Combine(ResourcesConstants.AgentInitsFolder, "FourWayStopTwoCarsGiveWayTest.csv")
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

        //               |      |
        //               |      |
        //               |      |
        //               |      |
        //_______________|      |_______________
        //                          2
        //
        //_______________        _______________
        //               |      |
        //               |     1|
        //               |      |
        //               |      |
        //               |      |
        //               |      |

        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        Assert.NotNull(table);

        //check that the car has come to a full stop
        var car1CloserThan10Meter = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                                 "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                                 "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
        Assert.InRange(Convert.ToDouble(car1CloserThan10Meter[1]["Velocity"]), 0.000, 0.001);

        var car2CloserThan10Meter = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                                 "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                                 "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");
        Assert.InRange(Convert.ToDouble(car2CloserThan10Meter[1]["Velocity"]), 0.000, 0.001);

        //check that car 1 crosses first
        var car1SecondEdge = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 41 AND " +
                                          "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

        var car2SecondEdge = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 31 AND " +
                                          "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

        Assert.True(Convert.ToInt32(car1SecondEdge.First()["Step"]) <=
                    Convert.ToInt32(car2SecondEdge.First()["Step"]));
    }

    [Fact]
    public void SingleCarComesToFullStopTest()
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
                EndPoint = start + TimeSpan.FromMinutes(2),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions =
                {
                    OutputPath = GetType().Name,
                    FileSuffix = nameof(SingleCarComesToFullStopTest)
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    File = ResourcesConstants.DriveGraphFourWayIntersection
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 1,
                    IndividualMapping =
                    {
                        new IndividualMapping { Name = "driveMode", Value = 6 },
                        new IndividualMapping { Name = "startLat", Value = 53.581086 },
                        new IndividualMapping { Name = "startLon", Value = 10.011879 },
                        new IndividualMapping { Name = "osmRoute", Value = "[22;41]" },
                        new IndividualMapping { Name = "trafficCode", Value = "south-african" }
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

        Assert.Equal(120, workflowState.Iterations);

        //               |      |
        //               |      |
        //               |      |
        //               |      |
        //_______________|      |_______________
        //                        
        //
        //_______________        _______________
        //               |      |
        //               |     1|
        //               |      |
        //               |      |
        //               |      |
        //               |      |

        var table = CsvReader.MapData(Path.Combine(GetType().Name,
            $"{nameof(CarDriver)}{nameof(SingleCarComesToFullStopTest)}.csv"));
        Assert.NotNull(table);

        //check that the car has come to a full stop
        var closerThanTenMeter = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                              "Convert(CurrentEdgeId, 'System.Int32') = 22");

        Assert.InRange(Convert.ToDouble(closerThanTenMeter[1]["Velocity"]), 0.000, 0.001);
    }
}