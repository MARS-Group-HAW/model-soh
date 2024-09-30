using System.Diagnostics;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;

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