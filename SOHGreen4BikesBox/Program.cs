using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Executor;
using Mars.Core.Executor.Implementation;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel;
using SOHModel.Bicycle.Rental;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Routing;

namespace SOHGreen4BikesBox;

/// <summary>
///     This pre-defined starter program runs the Green4Bike scenario with outside passed arguments or
///     a default simulation inputConfiguration with CSV output and trips.
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);
        var description = Startup.CreateModelDescription();

        using var application = args != null && args.Length != 0
            ? SimulationStarter.BuildApplication(description, args)
            : SimulationStarter.BuildApplication(description,
                SimulationConfig.Deserialize(File.ReadAllText("config.json")));

        var simulation = application.Resolve<ISimulation>();
        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();
        watch.Stop();
        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
    }
}