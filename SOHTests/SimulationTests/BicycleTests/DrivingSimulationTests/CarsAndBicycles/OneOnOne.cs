using System.Globalization;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.BicycleTests.Helper;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DrivingSimulationTests.CarsAndBicycles
{
    [Collection("SimulationTests")]
    public class OneOnOne
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public OneOnOne(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void OneBicycleOneSlowCarTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Info);
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/OneOnOneSlowCar.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ";");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "one-bicycle-one-slow-car-BICYCLE-trips",
                1, 0);

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ";");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(carTable, "one-bicycle-one-slow-car-CAR-trips",
                1, 0);
        }

        [Fact]
        public void OneOnOneTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Info);
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/OneOnOne.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ";");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "one-bicycle-one-car-BICYCLE-trips",
                1, 0);

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ";");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(carTable, "one-bicycle-one-car-CAR-trips",
                1, 0);
        }
    }
}