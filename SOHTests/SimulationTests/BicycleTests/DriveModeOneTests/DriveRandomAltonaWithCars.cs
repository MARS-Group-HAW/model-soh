using System.Globalization;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DriveModeOneTests
{
    [Collection("SimulationTests")]
    public class DriveRandomAltonaWithCars
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DriveRandomAltonaWithCars(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void DriveRandomAltonaWithCarsTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Info);
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
//            modelDescription.AddLayer<BicycleLayer>();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
//            modelDescription.AddAgent<CarDriver, BicycleLayer>();
//            modelDescription.AddAgent<Cyclist, BicycleLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/DriveRandomAltonaWithCars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);
        }
    }
}