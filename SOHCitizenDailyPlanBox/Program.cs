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
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel;
using SOHModel.Bicycle.Rental;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Layers;
using SOHModel.Multimodal.Layers.TrafficLight;
using SOHModel.Multimodal.Model;

namespace SOHCitizenDailyPlanBox;

internal static class Program
{
    private static void Main(string[] args)
    {
        var watch = Stopwatch.StartNew();
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);

        var description = Startup.CreateModelDescription();

        var application = args != null && args.Length != 0
            ? SimulationStarter.BuildApplication(description, args)
            : SimulationStarter.BuildApplication(description, GetConfig());
        
        var simulation = application.Resolve<ISimulation>();
        simulation.StartSimulation();
        watch.Stop();
        Console.WriteLine($"Complete execution lasted:           {watch.ElapsedMilliseconds}");
    }

    private static SimulationConfig GetConfig()
    {
        SimulationConfig simulationConfig;
        var configValue = Environment.GetEnvironmentVariable("CONFIG");

        if (configValue != null)
        {
            Console.WriteLine("Use passed simulation config by environment variable");
            simulationConfig = SimulationConfig.Deserialize(configValue);
            Console.WriteLine(simulationConfig.Serialize());
        }
        else
        {
            var file = File.ReadAllText("config_altona_altstadt.json");
            simulationConfig = SimulationConfig.Deserialize(file);
        }

        return simulationConfig;
    }
}