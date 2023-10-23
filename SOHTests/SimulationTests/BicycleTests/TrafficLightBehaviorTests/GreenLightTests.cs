using System;
using System.Globalization;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
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
    public class GreenLightTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public GreenLightTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CarAgentSeesGreenLightAndDoesNotBreakTest()
        {
            LoggerFactory.SetLogLevel(LogLevel.Warning);

            _testOutputHelper.WriteLine(Directory.GetCurrentDirectory());
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var modelDescription = new ModelDescription();

            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();
            modelDescription.AddLayer<StaticTrafficLightLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle/GreenLight.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);
            var starter = SimulationStarter.Start(modelDescription, simConfig);
            starter.Run();

            var table = CsvDataTableFactory.CreateDataTableFromCsv("Cyclist.csv", ",");
            Assert.NotNull(table);

            //make sure that the car has detected the traffic light and thinks that it is green
            var colorRes = table.Select("CurrentEdgeId = '1' AND" +
                                        " Convert(RemainingDistanceOnEdge, 'System.Decimal') < '40.0' AND" +
                                        " Convert(RemainingDistanceOnEdge, 'System.Decimal') > '0'");

            foreach (var dataRow in colorRes) Assert.Equal(3, Convert.ToInt32(dataRow["Color"]));

            //check if the car changes its speed after accelerating to max speed (this shouldn't happen)
            var speedRes = table.Select("CurrentEdgeId = '1' AND" + " Convert(Tick, 'System.Int32') > 50 AND " +
                                        "Convert(Tick, 'System.Int32') < 150");

            foreach (var dataRow in speedRes)
            {
                var velocity = Convert.ToDouble(dataRow["Velocity"]);
                Assert.InRange(velocity, 8, 9.1);
            }
        }
    }
}