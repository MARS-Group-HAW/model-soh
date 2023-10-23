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
    public class BicyclesOnStreet
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BicyclesOnStreet(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact(Skip = "Performance")]
        public void HundredCyclistsMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona100Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            for (var i = 0; i < 100; i++)
                SimpleResultPrinter.PrintResults(table,
                    "medium-altona-hundred-bic-street-bic" + i + "-trips",
                    2, i);
        }

        [Fact(Skip = "Performance")]
        public void HundredCyclistsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona100Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "hundred-bicycles-on-street-bic0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "hundred-bicycles-on-street-bic50-trips",
                2, 50);
            SimpleResultPrinter.PrintResults(table, "hundred-bicycles-on-street-bic99-trips",
                2, 99);
        }

        [Fact(Skip = "Performance")]
        public void OneCyclistTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona1Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "00000000-0000-0000-0000-000000000000", "bicycles-on-street-trips",
                2);
        }

        [Fact(Skip = "Performance")]
        public void TenCyclistsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona10Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "ten-bicycles-on-street-bic0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "ten-bicycles-on-street-bic3-trips",
                2, 3);
            SimpleResultPrinter.PrintResults(table, "ten-bicycles-on-street-bic9-trips",
                2, 9);
        }

        [Fact(Skip = "Performance")]
        public void ThousandCyclistsMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona800Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "medium-800-bicycles-on-street-bic0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "medium-800-bicycles-on-street-bic250-trips",
                2, 250);
            SimpleResultPrinter.PrintResults(table, "medium-800-bicycles-on-street-bic500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(table, "medium-800-bicycles-on-street-bic750-trips",
                2, 750);
            SimpleResultPrinter.PrintResults(table, "medium-800-bicycles-on-street-bic799-trips",
                2, 799);
        }

        [Fact(Skip = "Performance")]
        public void ThousandCyclistsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona500Bic0Cars.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-street-bic0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-street-bic500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(table, "thousand-bicycles-on-street-bic999-trips",
//                2, 999);

            for (var i = 0; i < 500; i++)
                SimpleResultPrinter.PrintResults(table,
                    "medium-altona-thousand-bic-street-bic" + i + "-trips",
                    2, i);
        }
    }
}