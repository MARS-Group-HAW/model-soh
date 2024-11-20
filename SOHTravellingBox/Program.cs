using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Domain.Graph;
using SOHModel.SemiTruck.Model;

namespace SOHSemiTruckSimulation
{
    /// <summary>
    ///     Program to start the semi-truck simulation on autobahns and bundesstraßen.
    /// </summary>
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
            LoggerFactory.SetLogLevel(LogLevel.Warning);

            // Initialize model description for semi-truck simulation
            var description = new ModelDescription();

            // Add SemiTruckLayer for truck driving routes
            description.AddLayer<SemiTruckLayer>(new[] { typeof(ISpatialGraphLayer) });
            
            // Add the scheduling layer to control spawning of trucks over time
            description.AddLayer<SemiTruckSchedulerLayer>();

            // Add SemiTruckDriver agent type for individual trucks
            description.AddAgent<SemiTruckDriver, SemiTruckLayer>();

            // Add SemiTruck entity to define truck properties
            description.AddEntity<SemiTruck>();

            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "resources");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            
            // Initialize simulation container and load configuration
            ISimulationContainer application;
            if (args != null && args.Length != 0)
            {
                var container = CommandParser.ParseAndEvaluateArguments(description, args);
                var config = container.SimulationConfig;
                
                application = SimulationStarter.BuildApplication(description, config);
            }
            else
            {
                // Specify the primary config file for the simulation
                var file = File.ReadAllText("config.json"); // or specify config_dammtor.json if needed
                var simConfig = SimulationConfig.Deserialize(file);
                application = SimulationStarter.BuildApplication(description, simConfig);
            }
            

            // Start simulation
            var simulation = application.Resolve<ISimulation>();

            var watch = Stopwatch.StartNew();
            var state = simulation.StartSimulation();

            watch.Stop();

            Console.WriteLine($"Executed iterations: {state.Iterations}");
            Console.WriteLine($"Simulation runtime: {watch.Elapsed}");
            
            
            application.Dispose();
        }
    }
}
