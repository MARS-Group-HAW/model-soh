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
using SOHModel.Bicycle.Rental;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Car.Rental;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;

namespace SOHChristmasMarketBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);

        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
        
        description.AddLayer<HumanTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<HumanTraveler, HumanTravelerLayer>>(
            "HumanTravelerSchedulerLayer");

        description.AddAgent<HumanTraveler, HumanTravelerLayer>();

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