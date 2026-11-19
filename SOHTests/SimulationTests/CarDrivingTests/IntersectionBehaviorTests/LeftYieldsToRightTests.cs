using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Common.Collections.Graph;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using SOHModel.Domain.Common;
using Xunit;

namespace SOHTests.SimulationTests.CarDrivingTests.IntersectionBehaviorTests;

public class SpatialGraphFixture : IDisposable
{
    public SpatialGraphFixture()
    {
        var env = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphFourWayIntersection);
        DriveGraphFourWayIntersection = env.Graph;

        env = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        DriveGraphAltonaAltstadt = env.Graph;
    }

    public SpatialGraph DriveGraphFourWayIntersection { get; }

    public SpatialGraph DriveGraphAltonaAltstadt { get; }

    public void Dispose()
    {
    }
}

[Collection("SimulationTests")]
public class LeftYieldsToRightTests : IClassFixture<SpatialGraphFixture>
{
    private readonly SpatialGraphFixture _graphFixture;

    public LeftYieldsToRightTests(SpatialGraphFixture graphFixture)
    {
        _graphFixture = graphFixture;
    }

    [Fact]
    public void CarReducesItsVelocityBeforeCrossingTest()
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
                    FileSuffix = nameof(CarReducesItsVelocityBeforeCrossingTest),
                    OutputPath = GetType().Name
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(_graphFixture.DriveGraphFourWayIntersection)
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
                        new IndividualMapping { Name = "osmRoute", Value = "[22;41]" }
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
            $"{nameof(CarDriver)}{nameof(CarReducesItsVelocityBeforeCrossingTest)}.csv"));
        Assert.NotNull(table);

        //check that the car has reduced its speed around the crossing
        // (edge 22 approaches the junction; edge 41 is the first edge after it)
        var closerThanTenMeter = table.Select(
            "Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
            "(Convert(CurrentEdgeId, 'System.Int32') = 22 OR Convert(CurrentEdgeId, 'System.Int32') = 41) AND " +
            "Convert(Velocity, System.Decimal) > 1");

        Assert.NotEmpty(closerThanTenMeter);
        const double tolerance = 0.5;
        Assert.Contains(closerThanTenMeter, row =>
        {
            var velocity = row["Velocity"].Value<double>();
            return velocity >= VehicleConstants.IntersectionSpeed - tolerance &&
                   velocity <= VehicleConstants.IntersectionSpeed + tolerance;
        });
    }

    [Fact]
    public void LeftYieldsToRightFourCarsTest()
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
                    FileSuffix = nameof(LeftYieldsToRightFourCarsTest)
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(_graphFixture.DriveGraphFourWayIntersection)
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 4,
                    File = Path.Combine(ResourcesConstants.AgentInitsFolder, "LeftYieldsToRightFourCarsTest.csv")
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
        //      straight |4     | straight
        //_______________|      |_______________
        //                        2
        //
        //_____________3_        _______________
        //     straight  |      |
        //               |     1| straight
        //               |      |
        //               |      |
        //               |      |
        //               |      |

        var table = CsvReader.MapData(Path.Combine(GetType().Name,
            $"{nameof(CarDriver)}{nameof(LeftYieldsToRightFourCarsTest)}.csv"));
        Assert.NotNull(table);

        //check that no deadlock occured
        var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 41 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
        Assert.True(car1.Any());

        var car2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 31 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");
        Assert.True(car2.Any());

        var car3 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 11 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");
        Assert.True(car3.Any());

        var car4 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 21 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a004'");
        Assert.True(car4.Any());
    }

    [Fact]
    public void LeftYieldsToRightThreeCarsTest()
    {
        //LoggerFactory.SetLogLevel(LogLevel.Off);
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
                    FileSuffix = nameof(LeftYieldsToRightThreeCarsTest)
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(_graphFixture.DriveGraphFourWayIntersection)
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 3,
                    File = Path.Combine(ResourcesConstants.AgentInitsFolder, "LeftYieldsToRightThreeCarsTest.csv")
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
        //               |      |straight
        //_______________|      |_______________
        //                        2
        //
        //_____________3_        _______________
        //    straight   |      |
        //               |     1| straight
        //               |      |
        //               |      |
        //               |      |
        //               |      |

        var table = CsvReader.MapData(Path.Combine(GetType().Name,
            $"{nameof(CarDriver)}{nameof(LeftYieldsToRightThreeCarsTest)}.csv"));
        Assert.NotNull(table);

        // last tick on each approach edge (DataTable.Select row order is not guaranteed)
        var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
        var car2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");
        var car3 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 32 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");

        Assert.NotEmpty(car1);
        Assert.NotEmpty(car2);
        Assert.NotEmpty(car3);

        var car1LeaveStep = car1.Max(row => Convert.ToInt32(row["Step"]));
        var car2LeaveStep = car2.Max(row => Convert.ToInt32(row["Step"]));
        var car3LeaveStep = car3.Max(row => Convert.ToInt32(row["Step"]));

        // car 2 has the right of way over car 1 (same-tick leave is allowed with discrete delta-t)
        Assert.True(car1LeaveStep >= car2LeaveStep,
            $"Expected car2 before/with car1; car1Leave={car1LeaveStep}, car2Leave={car2LeaveStep}");

        // car 3 yields to car 1 only while car 1 is within the give-way distance of the junction. Agents are
        // executed in arbitrary order, so car 1 may have stopped further back for car 2 already, letting car 3 pass.
        var car1BeforeCar3Left = table.Select($"Convert(Step, 'System.Int32') = {car3LeaveStep} AND " +
                                              "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
        var car1Waiting = car1BeforeCar3Left.Any(row =>
            Convert.ToInt32(row["CurrentEdgeId"]) == 22 &&
            Convert.ToDouble(row["RemainingDistanceOnEdge"]) < 10);
        Assert.True(car3LeaveStep >= car1LeaveStep || !car1Waiting,
            $"Car 3 crossed while car 1 was waiting at the junction; car3Leave={car3LeaveStep}, car1Leave={car1LeaveStep}");
    }

    [Fact]
    public void LeftYieldsToRightTrafficFromRightTest()
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
                    FileSuffix = nameof(LeftYieldsToRightTrafficFromRightTest)
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(_graphFixture.DriveGraphFourWayIntersection)
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 3,
                    File = Path.Combine(ResourcesConstants.AgentInitsFolder,
                        "LeftYieldsToRightTrafficFromRightTest.csv")
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

        var table = CsvReader.MapData(Path.Combine(GetType().Name,
            $"{nameof(CarDriver)}{nameof(LeftYieldsToRightTrafficFromRightTest)}.csv"));
        Assert.NotNull(table);


        //               |      |
        //               |      |
        //               |      |
        //               |      |
        //_______________|      |_______________
        //                        2
        //
        //_______________        _______________
        //               |      |
        //               |     1|
        //               |      |
        //               |      |
        //               |      |
        //               |      |


        // last tick on each approach edge (DataTable.Select row order is not guaranteed)
        var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
        var car2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

        Assert.NotEmpty(car1);
        Assert.NotEmpty(car2);

        var car1LeaveStep = car1.Max(row => Convert.ToInt32(row["Step"]));
        var car2LeaveStep = car2.Max(row => Convert.ToInt32(row["Step"]));

        // car 2 crosses first as it comes from the right (same-tick leave OK)
        Assert.True(car1LeaveStep >= car2LeaveStep,
            $"Expected car2 before/with car1; car1Leave={car1LeaveStep}, car2Leave={car2LeaveStep}");
    }
}
