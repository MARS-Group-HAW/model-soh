using System.Collections.Generic;
using System.IO;
using Mars.Common.Core.Logging;
using Mars.Common.Core.Logging.Enums;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHDomain.Output;
using SOHMultimodalModel.Layers.TrafficLight;
using SOHMultimodalModel.Output.Trips;
using SOHResources;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests.DriveModeThreeTests
{
    [Collection("SimulationTests")]
    public class DriveFromAtoBWithLights
    {
        [Fact]
        public void DriveFromAtoB()
        {
            LoggerFactory.SetLogLevel(LogLevel.Warning);

            var modelDescription = new ModelDescription();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddLayer<TrafficLightLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var path = Path.Combine(ResourcesConstants.SimConfigFolder, "bicycle-simulations",
                "DriveFromAtoB.json");
            var config = File.ReadAllText(path);
            var simConfig = SimulationConfig.Deserialize(config);

            var starter = SimulationStarter.Start(modelDescription, simConfig);
            var state = starter.Run();

            var modelAllActiveLayers = state.Model.AllActiveLayers;
            foreach (var layer in modelAllActiveLayers)
            {
                if (!(layer is CarLayer carLayer)) continue;
                var agents = carLayer.Driver.Values;
                if (agents is IEnumerable<ITripSavingAgent> tripSavingAgents)
                    TripsOutputAdapter.PrintTripResult(new List<ITripSavingAgent>(tripSavingAgents));
            }
        }
    }
}