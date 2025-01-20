using System;
using System.Collections.Generic;
using Mars.Interfaces.Model;
using SOHModel.Car.Model;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Layers.TrafficLight;
using SOHModel.Multimodal.Model;



namespace SOHC_ITSBox;

/// <summary>
///
/// 
/// 
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Info);
        
        var description = new ModelDescription();
        
        description.AddLayer<TrafficLightLayer>();
        description.AddLayer<CarLayer>();
        description.AddAgent<CarDriver, CarLayer>();
        description.AddAgent<EmergencyCarDriver, CarLayer>();
        description.AddEntity<Car>();
        
        
        ISimulationContainer application;
        
        if (args != null && args.Length != 0)
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
        application.Dispose();
    }
}