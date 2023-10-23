using System.Diagnostics;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHBusModel.Model;
using SOHBusModel.Route;
using SOHBusModel.Station;
using SOHDomain.Graph;
using SOHMultimodalModel.Model;

namespace SOHBusBox;

internal static class Program
{
    private static void Main(string[]? args)
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>();
        description.AddLayer<BusLayer>();
        description.AddLayer<BusSchedulerLayer>();
        description.AddLayer<BusStationLayer>();
        description.AddLayer<BusRouteLayer>(new[] { typeof(IBusRouteLayer) });
        // description.AddLayer<BusGtfsRouteLayer>(new[] {typeof(IBusRouteLayer)});

        description.AddLayer<PassengerTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>(
            "PassengerTravelerSchedulerLayer");

        description.AddAgent<BusDriver, BusLayer>();
        description.AddAgent<PassengerTraveler, PassengerTravelerLayer>();

        description.AddEntity<Bus>();

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