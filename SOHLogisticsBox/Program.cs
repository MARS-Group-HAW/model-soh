using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel.SemiTruck.Model;
using SOHModel.SemiTruck.Scheduling;

namespace SOHRoutingOptimization;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Warning);

        var description = new ModelDescription();

        // Add only the necessary layers for car simulation
        // description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
        description.AddLayer<SemiTruckLayer>();
        description.AddLayer<SemiTruckSchedulerLayer>();
        description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
        // Add car-related entities
        description.AddEntity<SemiTruck>();




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
            // var simConfig = CreateDefaultConfig();
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

   
