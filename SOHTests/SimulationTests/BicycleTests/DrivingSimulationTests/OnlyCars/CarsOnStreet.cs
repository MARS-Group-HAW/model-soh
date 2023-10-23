using System.Globalization;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHCarModel.Model;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHResources;
using SOHTests.SimulationTests.BicycleTests.Helper;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DrivingSimulationTests.OnlyCars
{
    [Collection("SimulationTests")]
    public class CarsOnStreet
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CarsOnStreet(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            //            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona0Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car999-trips",
                2, 999);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car1500-trips",
                2, 1500);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car2000-trips",
                2, 2000);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car2500-trips",
                2, 2500);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car3000-trips",
                2, 3000);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car3500-trips",
                2, 3500);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car4000-trips",
                2, 4000);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car4500-trips",
                2, 4500);
            SimpleResultPrinter.PrintResults(table, "medium-five-thousand-cars-on-street-car4999-trips",
                2, 4999);
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            //            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona0Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car999-trips",
                2, 999);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car1500-trips",
                2, 1500);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car2000-trips",
                2, 2000);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car2500-trips",
                2, 2500);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car3000-trips",
                2, 3000);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car3500-trips",
                2, 3500);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car4000-trips",
                2, 4000);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car4500-trips",
                2, 4500);
            SimpleResultPrinter.PrintResults(table, "five-thousand-cars-on-street-car4999-trips",
                2, 4999);
        }

        [Fact(Skip = "Performance")]
        public void ThousandCarsMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
//
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona0Bic1000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "medium-thousand-cars-on-street-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "medium-thousand-cars-on-street-car250-trips",
                2, 250);
            SimpleResultPrinter.PrintResults(table, "medium-thousand-cars-on-street-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(table, "medium-thousand-cars-on-street-car750-trips",
                2, 750);
            SimpleResultPrinter.PrintResults(table, "medium-thousand-cars-on-street-car999-trips",
                2, 999);
        }

//        [Fact (Skip="Performance")]
//        public void TenThousandCarsTest()
//        {
//            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
//            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
//            var modelDescription = new ModelDescription();
//            modelDescription.AddLayer<CarLayer>();
////            modelDescription.AddLayer<StaticTrafficLightLayer>();
//            modelDescription.AddAgent<CarDriver, CarLayer>();
//            modelDescription.AddLayer<TrafficLightLayer>();
////
//            var path = Path.Combine(SimulationTestConstants.SimConfigFolder,
//                "bicycle-simulations/Altona0Bic10000Cars-rushhour.json");
//            Assert.True(File.Exists(path));
//            var config    = File.ReadAllText(path);
//            var simConfig = SimulationConfig.Deserialize(config);
//
//            var starter       = SimulationStarter.Start(modelDescription, simConfig);
//            var workflowState = starter.Run();
//
//            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
//            Assert.NotNull(table);
//
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car999-trips",
//                2, 999);
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car2500-trips",
//                2, 2500);
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car5000-trips",
//                2, 5000);
//            SimpleResultPrinter.printResults(table, "ten-thousand-cars-on-street-car9999-trips",
//                2, 9999);
//        }

        [Fact(Skip = "Performance")]
        public void ThousandCarsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
//
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona0Bic1000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(table);

            SimpleResultPrinter.PrintResults(table, "thousand-cars-on-street-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(table, "thousand-cars-on-street-car250-trips",
                2, 250);
            SimpleResultPrinter.PrintResults(table, "thousand-cars-on-street-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(table, "thousand-cars-on-street-car750-trips",
                2, 750);
            SimpleResultPrinter.PrintResults(table, "thousand-cars-on-street-car999-trips",
                2, 999);
        }

//        [Fact (Skip="Performance")]
//        public void NineThousandCarsTest()
//        {
//            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
//            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
//            var modelDescription = new ModelDescription();
//            modelDescription.AddLayer<CarLayer>();
//            //            modelDescription.AddLayer<StaticTrafficLightLayer>();
//            modelDescription.AddAgent<CarDriver, CarLayer>();
//            modelDescription.AddLayer<TrafficLightLayer>();
//            //
//            var path = Path.Combine(SimulationTestConstants.SimConfigFolder,
//                "bicycle-simulations/Altona0Bic9000Cars-rushhour.json");
//            Assert.True(File.Exists(path));
//            var config    = File.ReadAllText(path);
//            var simConfig = SimulationConfig.Deserialize(config);
//
//            var starter       = SimulationStarter.Start(modelDescription, simConfig);
//            var workflowState = starter.Run();
//
//            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
//            Assert.NotNull(table);
//
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car999-trips",
//                2, 999);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car1500-trips",
//                2, 1500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car2000-trips",
//                2, 2000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car2500-trips",
//                2, 2500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car3000-trips",
//                2, 3000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car3500-trips",
//                2, 3500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car4000-trips",
//                2, 4000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car4500-trips",
//                2, 4500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car5000-trips",
//                2, 5000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car5500-trips",
//                2, 5500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car6000-trips",
//                2, 6000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car6500-trips",
//                2, 6500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car7000-trips",
//                2, 7000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car7500-trips",
//                2, 7500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car8000-trips",
//                2, 8000);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car8500-trips",
//                2, 8500);
//            SimpleResultPrinter.printResults(table, "nine-thousand-cars-on-street-car8999-trips",
//                2, 8999);
//        }
//        
//        [Fact (Skip="Performance")]
//        public void SevenThousandCarsTest()
//        {
//            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
//            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
//            var modelDescription = new ModelDescription();
//            modelDescription.AddLayer<CarLayer>();
//            //            modelDescription.AddLayer<StaticTrafficLightLayer>();
//            modelDescription.AddAgent<CarDriver, CarLayer>();
//            modelDescription.AddLayer<TrafficLightLayer>();
//            //
//            var path = Path.Combine(SimulationTestConstants.SimConfigFolder,
//                "bicycle-simulations/Altona0Bic7000Cars-rushhour.json");
//            Assert.True(File.Exists(path));
//            var config    = File.ReadAllText(path);
//            var simConfig = SimulationConfig.Deserialize(config);
//
//            var starter       = SimulationStarter.Start(modelDescription, simConfig);
//            var workflowState = starter.Run();
//
//            var table = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
//            Assert.NotNull(table);
//
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car999-trips",
//                2, 999);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car1500-trips",
//                2, 1500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car2000-trips",
//                2, 2000);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car2500-trips",
//                2, 2500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car3000-trips",
//                2, 3000);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car3500-trips",
//                2, 3500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car4000-trips",
//                2, 4000);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car4500-trips",
//                2, 4500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car5000-trips",
//                2, 5000);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car5500-trips",
//                2, 5500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car6000-trips",
//                2, 6000);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car6500-trips",
//                2, 6500);
//            SimpleResultPrinter.printResults(table, "seven-thousand-cars-on-street-car6999-trips",
//                2, 6999);
//        }
    }
}