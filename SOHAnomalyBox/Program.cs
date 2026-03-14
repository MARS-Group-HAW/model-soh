using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using Mars.Interfaces.Annotations;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.ChristmasMarket.Entities;
using SOHModel.ChristmasMarket.Layers;
using SOHModel.Domain.Graph;

namespace SOHAnomalyBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Info);

        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
        description.AddLayer<MarketLayer>();
        description.AddLayer<MarketTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<DesireMarketTraveler, MarketTravelerLayer>>("MarketTravelerSchedulerLayer");
        description.AddAgent<DesireMarketTraveler, MarketTravelerLayer>();
        description.AddAgent<MarketAnalyticsAgent, MarketTravelerLayer>();
        description.AddEntity<MarketStall>();

        ISimulationContainer application;
        SimulationConfig simConfig;
        
        if (args != null && args.Length != 0)
        {
            var container = CommandParser.ParseAndEvaluateArguments(description, args);
            simConfig = container.SimulationConfig;
            application = SimulationStarter.BuildApplication(description, simConfig);
        }
        else
        {
            var file = File.ReadAllText("config.json");
            simConfig = SimulationConfig.Deserialize(file);
            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        var simulation = application.Resolve<ISimulation>();

        Console.WriteLine("Starting simulation...");
        try
        {
            simulation.StartSimulation();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRASH DUMP]: {ex}");
        }
        Console.WriteLine("Simulation finished.");

        application.Dispose();
    }
}