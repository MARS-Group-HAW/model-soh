using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Services;
using Mars.Components.Starter;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Layers.Initialization;
using Mars.Interfaces.Model;
using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.IntersectionBehaviorTests
{
    [Collection("SimulationTests")]
    public class LeftYieldsToRightTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public LeftYieldsToRightTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void BicycleReducesItsVelocityBeforeCrossingTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/LeftYieldsToRightAloneAtIntersectionTest.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
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

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //check that the bicycle has reduced its speed before crossing
            var closerThanTenMeter = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 10 AND " +
                                                  "Convert(CurrentEdgeId, 'System.Int32') = 22");

            // TODO i feel like the print is the old velo from previous step
            // TODO velo will be decreased by driverRand which is random between 0 and 1 
            Assert.InRange(Convert.ToDouble(closerThanTenMeter[0]["Velocity"]), BicycleConstants.IntersectionSpeed - 1,
                BicycleConstants.IntersectionSpeed + 0.5);
        }

        [Fact]
        public void LeftYieldsToRightFourCarsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            //LoggerFactory.SetLogLevel(LogLevel.Off);
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<DummySpawnerLayer>();
            modelDescription.AddAgent<Cyclist, DummySpawnerLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/LeftYieldsToRightFourBicyclesTest.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
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

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //check that no deadlock occured
            var bicycle1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 41 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");
            Assert.True(bicycle1.Any());

            var bicycle2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 31 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");
            Assert.True(bicycle2.Any());

            var bicycle3 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 11 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");
            Assert.True(bicycle3.Any());

            var bicycle4 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 50 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 21 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a004'");
            Assert.True(bicycle4.Any());
        }

        // TODO only seems to work sometimes
        [Fact(Skip = "Assert.True() Failure")]
        public void LeftYieldsToRightThreeCarsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            //LoggerFactory.SetLogLevel(LogLevel.Off);
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<DummySpawnerLayer>();
            modelDescription.AddAgent<Cyclist, DummySpawnerLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/LeftYieldsToRightThreeBicyclesTest.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
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

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //select the last ticks in which the bicycles are on there respective first edge
            var bicycle1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 1 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

            var bicycle2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 1 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

            var bicycle3 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 1 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 32 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");

            var bicycle1Neu = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 41 AND " +
                                           "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

            var bicycle2Neu = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 31 AND " +
                                           "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

            var bicycle3Neu = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 11 AND " +
                                           "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003'");


//            Assert.Equal(1, Convert.ToInt32(bicycle1[^1]["Step"]));
//            Assert.Equal(1, Convert.ToInt32(bicycle1[0]["Step"]));

//            check that bicycle 2 crosses first as it comes from the right
//            Assert.True(Convert.ToInt32(bicycle1[0]["Step"]) >= Convert.ToInt32(bicycle2[0]["Step"]));
//            Assert.True(Convert.ToInt32(bicycle3[0]["Step"]) >= Convert.ToInt32(bicycle2[0]["Step"]));

//            Assert.Equal(1, Convert.ToInt32(bicycle1Neu[0]["Step"]));
            Assert.True(Convert.ToInt32(bicycle1Neu[0]["Step"]) >= Convert.ToInt32(bicycle2Neu[0]["Step"]));
            Assert.True(Convert.ToInt32(bicycle3Neu[0]["Step"]) >= Convert.ToInt32(bicycle2Neu[0]["Step"]));

            //check that bicycle 3 crosses after bicycle 1
//            Assert.True(Convert.ToInt32(bicycle3Neu[0]["Step"]) > Convert.ToInt32(bicycle1Neu[0]["Step"]));

            //check that bicycle 3 crosses after bicycle 1
            Assert.True(Convert.ToInt32(bicycle3[0]["Step"]) >= Convert.ToInt32(bicycle1[0]["Step"]));
        }

        [Fact]
        public void LeftYieldsToRightTrafficFromRightTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<DummySpawnerLayer>();
            modelDescription.AddAgent<Cyclist, DummySpawnerLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/LeftYieldsToRightTrafficFromRightTest.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(120, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
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


            //select the last ticks in which the bicycles are on there respective first edge
            var bicycle1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 1 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 22 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

            var bicycle2 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 1 AND " +
                                        "Convert(CurrentEdgeId, 'System.Int32') = 12 AND " +
                                        "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");

            var bicycle1Neu = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 41 AND " +
                                           "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001'");

            var bicycle2Neu = table.Select("Convert(CurrentEdgeId, 'System.Int32') = 31 AND " +
                                           "StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002'");


            //Check that bicycle 2 crosses first as it comes from the right
            Assert.True(Convert.ToInt32(bicycle1Neu[0]["Step"]) >= Convert.ToInt32(bicycle2Neu[0]["Step"]));
//            Assert.True(Convert.ToInt32(bicycle1[0]["Step"]) > Convert.ToInt32(bicycle2[0]["Step"]));
        }
    }

    public class DummySpawnerLayer : ISteppedActiveLayer
    {
        private long _currentTick;
        private RegisterAgent _register;
        private SpatialGraphEnvironment _spatialGraphEnvironment;
        private UnregisterAgent _unregister;

        public bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
            UnregisterAgent unregisterAgentHandle)
        {
            _register = registerAgentHandle;
            _unregister = unregisterAgentHandle;

            _spatialGraphEnvironment = new SpatialGraphEnvironment(layerInitData.LayerInitConfig.File);

            AgentManager.SpawnAgents<Cyclist>(layerInitData.AgentInitConfigs.First(), _register, _unregister,
                new List<ILayer> {this},
                new List<IEnvironment>
                {
                    _spatialGraphEnvironment
                });

            return true;
        }

        public long GetCurrentTick()
        {
            return _currentTick;
        }

        public void SetCurrentTick(long currentStep)
        {
            _currentTick = currentStep;
        }

        public void Tick()
        {
        }

        public void PreTick()
        {
        }

        public void PostTick()
        {
        }
    }
}