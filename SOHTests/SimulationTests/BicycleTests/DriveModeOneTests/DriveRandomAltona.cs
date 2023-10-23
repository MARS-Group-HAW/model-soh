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
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddEntity<Bicycle>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/DriveRandomAltona.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);

            // TODO One or more errors occurred. (One or more errors occurred. (Driving lane must be positive int, current value 3 is not suitable as lane count is 2 (Parameter 'DrivingLane')))
        }
    }
}