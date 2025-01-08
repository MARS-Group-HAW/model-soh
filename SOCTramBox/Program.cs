using System.Diagnostics;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Multimodal.Model;
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;

namespace SOCTramBox;

public class Program
{
    private static void Main(string[]? args)
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var description = new ModelDescription();

        // Core tram simulation layers
        description.AddLayer<TrainLayer>();
        description.AddLayer<TrainSchedulerLayer>();
        description.AddLayer<TrainStationLayer>();
        description.AddLayer<TrainRouteLayer>(new[] { typeof(ITrainRouteLayer) });

        // Passenger traveler layers
        description.AddLayer<PassengerTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>(
            "PassengerTravelerSchedulerLayer");

        // Define agents
        description.AddAgent<TrainDriver, TrainLayer>();
        description.AddAgent<PassengerTraveler, PassengerTravelerLayer>();

        // Define tram entities
        description.AddEntity<Train>();

        ISimulationContainer application;

        // Handle arguments or default to config.json
        if (args != null && args.Length != 0)
        {
            application = SimulationStarter.BuildApplication(description, args);
        }
        else
        {
            var file = File.ReadAllText("config.json");
            var simConfig = SimulationConfig.Deserialize(file);
            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();
        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
    }

}