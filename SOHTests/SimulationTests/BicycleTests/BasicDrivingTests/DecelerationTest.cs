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
    public class DecelerationTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DecelerationTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void BrakeFrom25To0Within25MeterTest()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/BrakeBicycleFrom25To0Within25Meter.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

            Assert.Equal(40, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            var res = table.Select("Convert(Velocity, 'System.Decimal')  >= '0' AND " +
                                   "Convert(CurrentEdgeId, 'System.Decimal') = '1'");
            Assert.InRange(Convert.ToDouble(res[0]["Velocity"]), 0, 0.5);
        }
    }
}