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

namespace SOHTests.SimulationTests.BicycleTests.InteractionTests.Cars
{
    [Collection("SimulationTests")]
    public class DriveRandomAltona
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DriveRandomAltona(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void DriveRandomAltonaTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Info);
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
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