using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Data.Wrapper.Relational;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.SemiTruck.Model;
using SOHModel.SemiTruck.RealTimeData;
using SOHModel.SemiTruck.Scheduling;



namespace SOHRoutingOptimization;

internal static class Program
{
    public static void Main(string[] args)
    {
        var watch = Stopwatch.StartNew();
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Warning);
        var description = new ModelDescription();

        
        description.AddLayer<SemiTruckLayer>();
        description.AddLayer<SemiTruckSchedulerLayer>();
        description.AddLayer<SemiTruckRealTimeLayer>();
        description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
        // Add semi-truck-related entities
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
            var configPath = Environment.GetEnvironmentVariable("MARS_CONFIG_PATH") ?? "config.json";
            var file = File.ReadAllText(configPath);
            var simConfig = SimulationConfig.Deserialize(file);
            
            
            // var simConfig = CreateDefaultConfig();
            application = SimulationStarter.BuildApplication(description, simConfig);
            
            
        }



        var simulation = application.Resolve<ISimulation>();
        
        var state = simulation.StartSimulation();

        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        application.Dispose();
    }
}

   
