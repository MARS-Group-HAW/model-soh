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
using SOHModel.ChristmasMarket;
using SOHModel.Domain.Graph;
using SOHModel.Domain.Model;
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
        
        description.AddLayer<MarketLayer>();
        description.AddLayer<MarketTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<RandomWalkMarketTraveler, MarketTravelerLayer>>("MarketTravelerSchedulerLayer");
        //description.AddLayer<AgentSchedulerLayer<SocialForcesMarketTraveler, MarketTravelerLayer>>("MarketTravelerSchedulerLayer");
        description.AddAgent<RandomWalkMarketTraveler, MarketTravelerLayer>();
        //description.AddAgent<SocialForcesMarketTraveler, MarketTravelerLayer>();
        
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