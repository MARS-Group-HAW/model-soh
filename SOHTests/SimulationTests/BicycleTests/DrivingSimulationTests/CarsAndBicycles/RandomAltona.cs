using System.Globalization;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHResources;
using SOHTests.SimulationTests.BicycleTests.Helper;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.DrivingSimulationTests.CarsAndBicycles
{
    [Collection("SimulationTests")]
    public class RandomAltona
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public RandomAltona(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsOneBicycleMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona1Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

            for (var i = 0; i < 5000; i++)
                SimpleResultPrinter.PrintResults(carTable,
                    "medium-altona-five-thousand-cars-on-street-one-bic-car" + i + "-trips",
                    2, i);

            //            for (int i = 0; i < 3000; i++)
//            {
            SimpleResultPrinter.PrintResults(bicTable,
                "medium-altona-five-thousand-cars-on-street-one-bic-bic" + 0 + "-trips",
                2, 0);
//            }
        }


        [Fact(Skip = "Performance")]
        public void FiveThousandCarsThousandBicyclesMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona1000Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car999-trips",
//                2, 999);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car1500-trips",
//                2, 1500);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car2000-trips",
//                2, 2000);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car2500-trips",
//                2, 2500);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car3000-trips",
//                2, 3000);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car3500-trips",
//                2, 3500);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car4000-trips",
//                2, 4000);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car4500-trips",
//                2, 4500);
//            SimpleResultPrinter.printResults(carTable, "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car4999-trips",
//                2, 4999);
            for (var i = 0; i < 5000; i++)
                SimpleResultPrinter.PrintResults(carTable,
                    "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car" + i + "-trips",
                    2, i);

            for (var i = 0; i < 1000; i++)
                SimpleResultPrinter.PrintResults(bicTable,
                    "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic" + i + "-trips",
                    2, i);

            //            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic0-trips",
//                2, 0);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic500-trips",
//                2, 500);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic1000-trips",
//                2, 1000);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic1500-trips",
//                2, 1500);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic2000-trips",
//                2, 2000);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic2500-trips",
//                2, 2500);
//            SimpleResultPrinter.printResults(bicTable,
//                "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic2999-trips",
//                2, 2999);
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsThousandBicyclesTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona1000Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car999-trips",
                2, 999);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car1500-trips",
                2, 1500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car2000-trips",
                2, 2000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car2500-trips",
                2, 2500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car3000-trips",
                2, 3000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car3500-trips",
                2, 3500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car4000-trips",
                2, 4000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car4500-trips",
                2, 4500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-thousand-bic-car4999-trips",
                2, 4999);

            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-thousand-bic-bic0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-thousand-bic-bic500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-thousand-bic-bic999-trips",
                2, 999);
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsThreeThousandBicyclesMediumAltonaTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona3000Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

            for (var i = 0; i < 5000; i++)
                SimpleResultPrinter.PrintResults(carTable,
                    "medium-altona-five-thousand-cars-on-street-three-thousand-bic-car" + i + "-trips",
                    2, i);

            for (var i = 0; i < 3000; i++)
                SimpleResultPrinter.PrintResults(bicTable,
                    "medium-altona-five-thousand-cars-on-street-three-thousand-bic-bic" + i + "-trips",
                    2, i);
        }

        [Fact(Skip = "Performance")]
        public void FiveThousandCarsThreeThousandBicyclesTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
//            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            //
            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Altona3000Bic5000Cars-rushhour.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car999-trips",
                2, 999);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car1500-trips",
                2, 1500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car2000-trips",
                2, 2000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car2500-trips",
                2, 2500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car3000-trips",
                2, 3000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car3500-trips",
                2, 3500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car4000-trips",
                2, 4000);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car4500-trips",
                2, 4500);
            SimpleResultPrinter.PrintResults(carTable, "five-thousand-cars-on-street-three-thousand-bic-car4999-trips",
                2, 4999);

            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic0-trips",
                2, 0);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic500-trips",
                2, 500);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic1000-trips",
                2, 1000);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic1500-trips",
                2, 1500);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic2000-trips",
                2, 2000);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic2500-trips",
                2, 2500);
            SimpleResultPrinter.PrintResults(bicTable, "five-thousand-cars-on-street-three-thousand-bic-bic2999-trips",
                2, 2999);
        }

        [Fact(Skip = "Performance")]
        public void TwoThousandCyclistsBikePathThousandCarsTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<BicycleLayer>();
            modelDescription.AddAgent<Cyclist, BicycleLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle-simulations/Medium-Altona2000Bic1000Cars-rushhour.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            var carTable = CsvDataTableFactory.CreateDataTableFromCsv("CarDriver.csv", ",");
            Assert.NotNull(carTable);
            var bicTable = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(bicTable);

            for (var i = 0; i < 1000; i++)
                SimpleResultPrinter.PrintResults(carTable,
                    "medium-altona-thousand-cars-on-street-two-thousand-bic-bike-path-car" + i + "-trips",
                    2, i);

            for (var i = 0; i < 2000; i++)
                SimpleResultPrinter.PrintResults(bicTable,
                    "medium-altona-thousand-cars-on-street-two-thousand-bic-bike-path-bic" + i + "-trips",
                    2, i);
        }
    }
}