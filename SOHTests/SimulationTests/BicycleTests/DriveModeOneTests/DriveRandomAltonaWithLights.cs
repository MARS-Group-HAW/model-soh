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
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DriveModeOneTests
{
    [Collection("SimulationTests")]
    public class DriveRandomAltonaWithLights
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DriveRandomAltonaWithLights(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        // TODO lane seems to be set wrong some time (set to 2 when count is 2 (so max 1))
        [Fact]
        public void DriveRandomAltonaWithLightsTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Warning);
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();

            var path = ResourcesConstants,
                "bicycle/DriveRandomAltonaWithLights.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);
        }
    }
}