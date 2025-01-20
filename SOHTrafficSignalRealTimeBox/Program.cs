using System.Globalization;
using Mars.Common.Core.Logging;

namespace SOHTrafficSignalRealTimeBox;

/// <summary>
///     This pre-defined starter program runs the the Green4Bike scenario with outside passed arguments or
///     a default simulation inputConfiguration with CSV output and trips.
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Off);
        
        SensorThingsImporter.LoadData();
        
        /*
        var description = new ModelDescription();
         

        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
        description.AddLayer<GatewayLayer>();
        description.AddLayer<BicycleRentalLayer>();
        description.AddLayer<TrafficSignalLayer>();
        description.AddLayer<CycleTravelerLayer>();
        description.AddLayer<CycleTravelerSchedulerLayer>();

        description.AddAgent<CycleTraveler, CycleTravelerLayer>();
        description.AddEntity<RentalBicycle>();

        ISimulationContainer application;

        if (args != null && args.Length != 0)
        {
            application = SimulationStarter.BuildApplication(description, args);
        }
        else
        {
            var file = File.ReadAllText("config.json");
            var simConfig = SimulationConfig.Deserialize(file);
            
            var data = SensorThingsImporter.LoadData();
            var trafficSignalLayer = simConfig.LayerMappings.FirstOrDefault(mapping => 
                mapping.Name == nameof(TrafficSignalLayer));
            
            trafficSignalLayer?.Inputs.Add(new Input
            {
                Value = data
            });
            
            var data2 = SensorThingsImporter.LoadData();
            var rentalLayerMapping = simConfig.LayerMappings.First(mapping => 
                mapping.Name == nameof(BicycleRentalLayer));
            
            rentalLayerMapping.Inputs.Add(new Input
            {
                Value = data2
            });
            
            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();
        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        application.Dispose();
        */
    }
}