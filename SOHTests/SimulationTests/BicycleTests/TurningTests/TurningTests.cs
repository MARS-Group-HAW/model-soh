using System;
using System.Globalization;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.TurningTests
{
    [Collection("SimulationTests")]
    public class TurningTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TurningTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void SlowDownBeforeLeftTurnTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddEntity<Bicycle>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/LeftTurnTests.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(100, workflowState.Iterations);
//            Assert.Equal(1.88, BicycleConstants.SharpTurnSpeed);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //check that speed is below allowed turning speed for turn 1
            var restTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
                                         "Convert(CurrentEdgeId, 'System.Int32') = 1 AND " +
                                         "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 2");

            var currentVelocity = 0D;
            foreach (var row in restTurn1)
            {
                currentVelocity = Convert.ToDouble(row["Velocity"]);
                Assert.True(currentVelocity <= BicycleConstants.RegularTurnSpeed + 0.5);
            }

            //check that the car accelerates after the first turning maneuver
            var speedUpTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND  " +
                                            "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
                                            "Convert(PositionOnEdge, 'System.Decimal') > 5");

            currentVelocity = Convert.ToDouble(speedUpTurn1[0]["Velocity"]);
//            Assert.Equal(1, currentVelocity);

            Assert.True(currentVelocity > BicycleConstants.SharpTurnSpeed + 0.01);

            //check that speed is below allowed turning speed for turn 2
            var restTurn2 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
                                         "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
                                         "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 2");
            currentVelocity = Convert.ToDouble(restTurn2[0]["Velocity"]);
            Assert.True(currentVelocity < BicycleConstants.RegularTurnSpeed + 0.1);
        }
    }
}