using System;
using System.Globalization;
using System.IO;
using Mars.Common.Core;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.LaneChangingTests
{
    [Collection("SimulationTests")]
    public class ChangeLaneTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ChangeLaneTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ChangeLane()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            
            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/ChangeLaneTest.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

//            Assert.Equal(100, workflowState.Iterations);

//            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
//            Assert.NotNull(table);
//
//            //check that speed is below allowed turning speed for turn 1
//            var restTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
//                                         "Convert(CurrentEdgeId, 'System.Int32') = 1 AND " +
//                                         "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 4");
//
////            Assert.Single(restTurn1);
//            var currentVelocity = Convert.ToDouble(restTurn1[0]["Velocity"]);
//            Assert.InRange(currentVelocity, BicycleConstants.RegularTurnSpeed - 1.5, BicycleConstants.RegularTurnSpeed + 0.1);
////            Assert.True(currentVelocity < BicycleConstants.RegularTurnSpeed + 0.1);
//
//            //check that the car accelerates after the first turning maneuver
//            var speedUpTurn1 = table.Select("Convert(Tick, 'System.Int32') > 1 AND  " +
//                                            "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
//                                            "Convert(PositionOnEdge, 'System.Decimal') > 4");
//            currentVelocity = Convert.ToDouble(speedUpTurn1[0]["Velocity"]);
//            Assert.True(currentVelocity >= BicycleConstants.RegularTurnSpeed - 0.5);
//
//            //check that speed is below allowed turning speed for turn 2
//            var restTurn2 = table.Select("Convert(Tick, 'System.Int32') > 1 AND " +
//                                         "Convert(CurrentEdgeId, 'System.Int32') = 2 AND " +
//                                         "Convert(RemainingDistanceOnEdge, 'System.Decimal') < 4");
//            currentVelocity = Convert.ToDouble(restTurn2[0]["Velocity"]);
//            Assert.True(currentVelocity < BicycleConstants.RegularTurnSpeed + 0.1);
        }
    }
}