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

// ReSharper disable ClassNeverInstantiated.Global

namespace SOHTests.SimulationTests.BicycleTests.TrafficLightBehaviorTests
{
    [Collection("SimulationTests")]
    public class RedLightTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public RedLightTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void StopAtRedLightExperiment()
        {
            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder,
                "bicycle/BrakeBicycleFrom25To0Within100Meter.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var workflowState = starter.Run();

//            Assert.Equal(40, workflowState.Iterations);

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            var res = table.Select("Convert(Tick, 'System.Int32') > '1' AND " +
                                   "Convert(Velocity, 'System.Decimal')  < '1'");
//            Assert.Equal("16", res[0]["Step"]);

            var car1 = table.Select("Convert(RemainingDistanceOnEdge, System.Decimal) < 40 AND " +
                                    "Convert(CurrentEdgeId, 'System.Int32') = 1");

            foreach (var dataRow in car1) Assert.Equal(1, Convert.ToInt32(dataRow["Color"]));
        }
    }
}