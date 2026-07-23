using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;

namespace SOHCarletonDrivingBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);

        var description = new ModelDescription();
        description.AddLayer<CarletonCarLayer>("CarLayer");
        description.AddAgent<CarletonCarDriver, CarletonCarLayer>();
        description.AddEntity<Car>();

        SimulationConfig simConfig;
        ISimulationContainer application;
        if (args is { Length: > 0 })
        {
            var configPath = args[0];
            var configText = File.ReadAllText(configPath);
            simConfig = SimulationConfig.Deserialize(configText);
            if (configText.Contains("CarletonCarDriverSchedulerLayer", StringComparison.Ordinal))
                description.AddLayer<CarletonCarDriverSchedulerLayer>();
        }
        else
        {
            simConfig = SimulationConfig.Deserialize(File.ReadAllText("config.json"));
            description.AddLayer<CarletonCarDriverSchedulerLayer>();
        }

        // Default configs keep console:true. MARS ProgressBar needs a real console;
        // redirected / no-TTY shells throw — only then disable progress.
        if (simConfig.Globals.ShowConsoleProgress && !CanUseConsoleProgressBar())
        {
            Console.WriteLine(
                "[Carleton] No interactive console — disabling MARS progress bar " +
                "(globals.console / ShowConsoleProgress).");
            simConfig.Globals.ShowConsoleProgress = false;
        }

        application = SimulationStarter.BuildApplication(description, simConfig);

        var outputDir = simConfig.Globals.CsvOptions?.OutputPath;
        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = "results";
        Directory.CreateDirectory(outputDir);
        // Scenario 11 (and any run with breakdowns enabled) writes one row per event here.
        CarletonCarLayer.BreakdownLogPath = Path.Combine(outputDir, "breakdowns.csv");
        if (File.Exists(CarletonCarLayer.BreakdownLogPath))
            File.Delete(CarletonCarLayer.BreakdownLogPath);

        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();
        watch.Stop();

        application.Dispose();

        MoveTripsGeojson(outputDir);

        Console.WriteLine($"Output folder: {Path.GetFullPath(outputDir)}");
        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
    }

    /// <summary>
    /// True when console progress APIs are usable (required by MARS ProgressBar).
    /// </summary>
    private static bool CanUseConsoleProgressBar()
    {
        try
        {
            _ = Console.CursorTop;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>MARS writes trips on dispose; move into the same folder as CarletonCarDriver.csv.</summary>
    private static void MoveTripsGeojson(string outputDir)
    {
        foreach (var fileName in new[] { "CarletonCarDriver_trips.geojson", "CarDriver_trips.geojson" })
        {
            var dest = Path.Combine(outputDir, fileName);
            foreach (var src in new[] { fileName, Path.Combine("results", fileName) })
            {
                if (!File.Exists(src))
                    continue;
                if (Path.GetFullPath(src) == Path.GetFullPath(dest))
                    return;
                Directory.CreateDirectory(outputDir);
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Move(src, dest);
                return;
            }
        }
    }
}
