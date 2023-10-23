using System.Diagnostics;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHDomain.Graph;
using SOHMultimodalModel.Model;
using SOHTrainModel.Model;
using SOHTrainModel.Route;
using SOHTrainModel.Station;

namespace SOHTrainBox;

internal static class Program
{
    private static void Main(string[]? args)
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>();
        description.AddLayer<TrainLayer>();
        description.AddLayer<TrainSchedulerLayer>();
        description.AddLayer<TrainStationLayer>();
        description.AddLayer<TrainRouteLayer>(new[] { typeof(ITrainRouteLayer) });
        // description.AddLayer<TrainGtfsRouteLayer>(new[] {typeof(ITrainRouteLayer)});

        description.AddLayer<PassengerTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>(
            "PassengerTravelerSchedulerLayer");

        description.AddAgent<TrainDriver, TrainLayer>();
        description.AddAgent<PassengerTraveler, PassengerTravelerLayer>();

        description.AddEntity<Train>();

        ISimulationContainer application;
        if (args != null && args.Any())
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

        // var (_, layer) = state.Model.Layers.FirstOrDefault(pair => pair.Value is PassengerTravelerLayer);
        // if (layer != null)
        // {
        //     var agents = ((PassengerTravelerLayer)layer).Agents.Values;
        //     TripsOutputAdapter.PrintTripResult(agents);
        // }
    }
}