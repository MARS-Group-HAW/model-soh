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

        //check that the car has reduced its speed before crossing
        var closerThanTenMeter = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                              "Convert(CurrentEdgeId, 'System.Int32') = 22");

        Assert.True(closerThanTenMeter[0]["Velocity"].Value<double>() < VehicleConstants.IntersectionSpeed + 0.1);
        Assert.True(closerThanTenMeter[0]["Velocity"].Value<double>() > VehicleConstants.IntersectionSpeed - 0.1);
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

        //select the last ticks in which the cars are on there respective first edge
        var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

        var car2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

        var car3 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 32 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");


        //check that car 2 crosses first as it comes from the right
        Assert.True(Convert.ToInt32(car1[0]["Step"]) > Convert.ToInt32(car2[0]["Step"]));
        Assert.True(Convert.ToInt32(car3[0]["Step"]) > Convert.ToInt32(car2[0]["Step"]));

        //check that car 1 crosses after car 2
        Assert.True(Convert.ToInt32(car3[0]["Step"]) > Convert.ToInt32(car1[0]["Step"]));
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


        //select the last ticks in which the cars are on there respective first edge
        var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

        var car2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

        //Check that car 2 crosses first as it comes from the right
        Assert.True(Convert.ToInt32(car1[0]["Step"]) > Convert.ToInt32(car2[0]["Step"]));
    }
}