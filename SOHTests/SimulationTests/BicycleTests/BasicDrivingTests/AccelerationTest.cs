using System;
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

namespace SOHTests.SimulationTests.BicycleTests.BasicDrivingTests
{
    [Collection("SimulationTests")]
    public class AccelerationTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AccelerationTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void AccelerateFrom0To15Test()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddEntity<Bicycle>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/AccelerateBicycleFrom0To15Test.json");
            Assert.True(File.Exists(path));
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(210, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);
            var res = table.Select("Velocity >= '0'");
            Assert.NotNull(res);
            Assert.Equal("0", res[0]["Velocity"]);
            double velocity;
            for (var i = 1; i < res.Length; i++)
            {
                velocity = Convert.ToDouble(res[i]["Velocity"]);
                Assert.True(velocity > 0);
            }

            velocity = Convert.ToDouble(res[^1]["Velocity"]);
            Assert.True(velocity > 4);
        }
    }
}