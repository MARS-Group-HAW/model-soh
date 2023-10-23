using System.Globalization;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHResources;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.TrafficLightBehaviorTests
{
    [Collection("SimulationTests")]
    public class TrafficLightIntersectionTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TrafficLightIntersectionTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ThreeCarsIntersectionWithLightsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            LoggerFactory.SetLogLevel(LogLevel.Info);
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/TrafficLightIntersectionTests.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();
//            Assert.Equal(120, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //make sure that the cars reach the goal
            var bicycle1ReachesGoal =
                table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a001' AND GoalReached = 'True'");
            var bicycle2ReachesGoal =
                table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a002' AND GoalReached = 'True'");
            var bicycle3ReachesGoal =
                table.Select("StableId = '64eae14b-3976-4dd1-b324-e73f1e70a003' AND GoalReached = 'True'");
            Assert.NotEmpty(bicycle1ReachesGoal);
            Assert.NotEmpty(bicycle2ReachesGoal);
            Assert.NotEmpty(bicycle3ReachesGoal);
        }
    }
}