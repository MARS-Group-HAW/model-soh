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
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.BigEvent;

namespace SOHBigEventBox;

/// <summary>
/// 
///     !!!!2024-10-18: SLIGHTLY EDITED COPY OF SOHTravellingBox/Program.cs!!!!
///     
///     This pre-defined starter program runs the the
///     <value>Kellinghusen scenario</value>
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
        
        description.AddLayer<HumanTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<Visitor, HumanTravelerLayer>>(
            "HumanTravelerSchedulerLayer");

        description.AddAgent<Visitor, HumanTravelerLayer>();

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