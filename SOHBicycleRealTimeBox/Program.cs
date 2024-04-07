using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Bicycle.Rental;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Routing;

namespace SOHBicycleRealTime;

/// <summary>
///     SIGSIM paper scenario implementation.
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);

        var description = new ModelDescription();
        description.AddLayer<StreetLayer>(new[] { typeof(ISpatialGraphLayer) });
        description.AddLayer<GatewayLayer>();
        description.AddLayer<BicycleRentalLayer>();
        description.AddLayer<CycleTravelerLayer>();
        description.AddLayer<SidewalkLayer>();
        description.AddLayer<CycleTravelerSchedulerLayer>();

        description.AddAgent<CycleTraveler, CycleTravelerLayer>();
        description.AddEntity<RentalBicycle>();

        GlobalConfig.OutputFrequency = 60;

        //var config = ScenarioRealtime.Get();

        var configuration = CommandParser.ParseAndEvaluateArguments(description, args);
        var config = configuration.SimulationConfig;

        var layerMapping = config.LayerMappings.First(mapping => mapping.Name == "BicycleRentalLayer");
        if (string.IsNullOrEmpty(layerMapping.File))
        {
            layerMapping.Value = SensorThingsImporter.LoadData(config.Globals.StartPoint, config.Globals.EndPoint);
        }

        Console.WriteLine("Start executing scenario ... ");
        RunScenario(description, config);


        // Console.WriteLine("Start executing scenario A -- without agents");
        // var config = ScenarioA.Get();
        // RunScenario(description, config, "A");
        // Console.WriteLine("Finished with executing scenario A ");
        // Console.WriteLine();
        // Console.WriteLine();

        // for (int i = 1; i <= 1; i++)
        // {
        //     Console.WriteLine($"Iteration: {i}");
        //     Console.WriteLine();
        //     
        //     config.SimulationRunIteration = i;
        //     
        //     Console.WriteLine("Start executing scenario B -- default run with agents");
        //     config = ScenarioB.Get();
        //     RunScenario(description, config, "B");
        //     Console.WriteLine("Finished with executing scenario B ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //
        //     Console.WriteLine("Start executing scenario C -- default run with agents and leveling off phase");
        //     config = ScenarioC.Get();
        //     RunScenario(description, config, "C");
        //     Console.WriteLine("Finished with executing scenario C ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //     
        //     Console.WriteLine("Start executing scenario D -- run with correction at 17:30");
        //     config = ScenarioD.Get();
        //     RunScenario(description, config, "D");
        //     Console.WriteLine("Finished with executing scenario D ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //     
        //     Console.WriteLine("Start executing scenario E -- run with correction at 17:30 and leveling off phase");
        //     config = ScenarioE.Get();
        //     RunScenario(description, config, "E");
        //     Console.WriteLine("Finished with executing scenario E ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //     
        //     Console.WriteLine("Start executing scenario F -- run with multiple corrections after 17:30");
        //     config = ScenarioF.Get();
        //     RunScenario(description, config, "F");
        //     Console.WriteLine("Finished with executing scenario F ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //     
        //     Console.WriteLine(
        //         "Start executing scenario G -- run from 8:00 until 20:00 with correction after each hour");
        //     config = ScenarioG.Get();
        //     RunScenario(description, config, "G");
        //     Console.WriteLine("Finished with executing scenario G ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        //     
        //     Console.WriteLine("Start executing scenario H -- long run without any correction");
        //     config = ScenarioH.Get();
        //     RunScenario(description, config, "H");
        //     Console.WriteLine("Finished with executing scenario H ");
        //     Console.WriteLine();
        //     Console.WriteLine();
        // }
    }

    private static void RunScenario(ModelDescription description, SimulationConfig config)
    {
        var application = SimulationStarter.BuildApplication(description, config);

        CreateTrips(application);
    }

    private static void CreateTrips(ISimulationContainer application)
    {
        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();

        var layers = state.Model.Layers;

        foreach (var layer in layers)
            if (layer.Value is CycleTravelerLayer cycleTravelerLayer)
                Console.WriteLine(cycleTravelerLayer.RentalCount);

        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        application.Dispose();
    }
}

public static class GlobalConfig
{
    public static int OutputFrequency { get; set; }
}