using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Demonstration;
using SOHModel.Domain.Graph;

namespace SOHDemonstrationBox
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
            LoggerFactory.SetLogLevel(LogLevel.Info);
            
            var description = new ModelDescription();
            
            //Bewegungsnetzwerk (hier Fußgängerrouten unseres Bildausschnitts)
            description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
            
            //Hier leben alle Agententypen
            description.AddLayer<DemonstrationLayer>();
            //Scheduler der Demonstranten
            description.AddLayer<DemonstratorSchedulerLayer>();
            //description.AddLayer<PoliceSchedulerLayer>();
            
            description.AddAgent<PoliceChief, DemonstrationLayer>();
            description.AddAgent<Police, DemonstrationLayer>();
            description.AddAgent<Demonstrator, DemonstrationLayer>();
            description.AddAgent<RadicalDemonstrator, DemonstrationLayer>();

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
}