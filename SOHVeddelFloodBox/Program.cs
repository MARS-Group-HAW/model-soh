using System;
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

namespace SOHVeddelFloodBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);

        var description = new ModelDescription();

        // All environments where the agent move on resolve routes.
        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });

        // All agent layers
        description.AddLayer<HouseholdLayer>("HouseholdLayer");
        description.AddLayer<WaterLevelLayer>("aaa");
        description.AddAgent<VeddelTraveler, WaterLevelLayer>();
        
        for (var i = 1; i <= 1; i++)
        {
            ISimulationContainer application;
            if (args != null && args.Any())
            {
                var container = CommandParser.ParseAndEvaluateArguments(description, args);

                var config = container.SimulationConfig;
                config.SimulationRunIteration = i;

                if (i == 0) config.Globals.PostgresSqlOptions.OverrideByConflict = false;
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
}