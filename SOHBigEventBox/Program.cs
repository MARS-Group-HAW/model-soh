using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Bicycle.Model;
using SOHModel.Bicycle.Parking;
using SOHModel.Domain.Graph;
using SOHModel.BigEvent;
using SOHModel.Car.Model;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;
using SOHModel.Train.Model;
using SOHModel.Train.Station;
using SOHModel.Train.Route;

namespace SOHBigEventBox;

/// <summary>
///     This pre-defined starter program runs the
///     <b>Big Event scenario</b>
///     with outside passed arguments or
///     a default simulation inputConfiguration with CSV output and trips.
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var description = new ModelDescription();

        description.AddLayer<SpatialGraphMediatorLayer>([typeof(ISpatialGraphLayer)]);
        description.AddLayer<BaseWalkingLayer>();
        description.AddLayer<AgentSchedulerLayer<Visitor, BaseWalkingLayer>>("VisitorSchedulerLayer");
        description.AddLayer<AgentSchedulerLayer<Resident, BaseWalkingLayer>>("ResidentSchedulerLayer");
        description.AddLayer<BicycleParkingLayer>();
        description.AddLayer<BarclaysParkingLayer>();
        description.AddLayer<BusLayer>();
        description.AddLayer<BusRouteLayer>([typeof(IBusRouteLayer)]);
        // description.AddLayer<BusGtfsRouteLayer>([typeof(IBusRouteLayer)]);
        description.AddLayer<BusSchedulerLayer>();
        description.AddLayer<BusStationLayer>();

        description.AddAgent<Resident, BaseWalkingLayer>();
        description.AddAgent<Visitor, BaseWalkingLayer>();
        description.AddAgent<BusDriver, BusLayer>();

        description.AddEntity<Bicycle>();
        description.AddEntity<Car>();
        description.AddEntity<Bus>();
        
        ISimulationContainer application;
        if (args != null && args.Length != 0)
        {
            var container = CommandParser.ParseAndEvaluateArguments(description, args);
            var config = container.SimulationConfig;
            application = SimulationStarter.BuildApplication(description, config);
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
        application.Dispose();
    }
}