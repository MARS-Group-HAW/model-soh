using System.Globalization;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using SOHTests.SimulationTests.Commons;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SimulationTests.BicycleTests.SpeedLimitTests
{
    [Collection("SimulationTests")]
    public class IncreasingSpeedLimitTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public IncreasingSpeedLimitTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void AccelerateFrom10To25Test()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

//            LoggerFactory.SetLogLevel(LogLevel.Info);
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/AccelerateFrom10To25Test.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(630, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //first tick where max speed should be 30 km/h
            var res = table.Select("Tick = '1'");
            Assert.Single(res);
//            Assert.Equal("2.78", res[0]["MaxSpeed"]);

            //make sure car doesn't drive faster than allowed but also close to max speed of 8.33 m/s
            res = table.Select("Tick = '240' AND CurrentEdgeId = '1'");
            Assert.Single(res);
            var currentVelocity = double.Parse((string) res[0]["Velocity"]);
            Assert.InRange(currentVelocity, 2.6, 2.78);

            //first step after crossing the intersection when max speed has increased to 50 km/h
            var rows = table.Select("CurrentEdgeId = '2' AND Velocity > '2.6'");
//            Assert.Single(rows);
            currentVelocity = double.Parse((string) rows[0]["Velocity"]);
            Assert.InRange(currentVelocity, 2.6, 3);
//            Assert.Equal("13.89", rows[0]["MaxSpeed"]);

            //make sure car doesn't drive faster than allowed but also close to max speed of 13.89 m/s
            res = table.Select("Tick = '630' AND CurrentEdgeId = '2'");
            Assert.Single(res);
            currentVelocity = double.Parse((string) res[0]["Velocity"]);
            Assert.InRange(currentVelocity, 6.7, 6.944);
        }
    }
}