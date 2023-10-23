using System.Globalization;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.BicycleTests.Helper;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DrivingSimulationTests.OnlyBicycles
{
    [Collection("SimulationTests")]
    public class BicyclesOnCyclePath
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BicyclesOnCyclePath(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact(Skip = "Peformance")]
        public void ThousandCyclistsBikePathTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<BicycleLayer>();
            modelDescription.AddAgent<Cyclist, BicycleLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona1000Bic0Cars-rushhour-bikePath.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            for (var i = 0; i < 1000; i++)
                SimpleResultPrinter.PrintResults(table,
                    "medium-altona-thousand-bic-Bike-path-bic" + i + "-trips",
                    2, i);

            //
//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-bikePath-bic0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-street-bic500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-street-bic999-trips",
//                2, 999);
        }
    }
}