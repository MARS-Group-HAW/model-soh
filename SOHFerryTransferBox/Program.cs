using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel;
using SOHModel.Domain.Graph;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Route;
using SOHModel.Ferry.Station;
using SOHModel.Multimodal.Model;

namespace SOHFerryTransferBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Info);


        var description = Startup.CreateModelDescription();
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
    }
}