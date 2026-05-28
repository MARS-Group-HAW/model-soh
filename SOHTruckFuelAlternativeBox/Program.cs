using System.Diagnostics;
using System.Globalization;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.SemiTruck.Model;
using SOHModel.Database;

namespace TruckFuelAlternativeBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        DotNetEnv.Env.Load();

        // Initialize PostgreSQL logger for simulation data
        PostgresDbLogger.Instance = new PostgresDbLogger()
            .Register<RestEntity>("rests")
            .Register<VehicleSteeringDecisionEntity>("vehicle_steering_decision")
            .Register<VehicleSteeringPhysicsEntity>("vehicle_steering_physics")
            .Register<FuelConsumptionEntity>("fuel_consumption")
            .Register<RoadLoadEntity>("road_load")
            .ClearAllTables();

        var watch = Stopwatch.StartNew();
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Info);
        var description = new ModelDescription();


        description.AddLayer<SemiTruckLayer>();
        // description.AddLayer<SemiTruckSchedulerLayer>();

        description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
        // Add semi-truck-related entities
        description.AddEntity<SemiTruck>();

        ISimulationContainer application;
        if (args.Length != 0)
        {
            Console.WriteLine("default config");
            var container = CommandParser.ParseAndEvaluateArguments(description, args);
            var config = container.SimulationConfig;
            application = SimulationStarter.BuildApplication(description, config);
        }
        else
        {
            var configPath = Environment.GetEnvironmentVariable("MARS_CONFIG_PATH") ?? "config.json";
            Console.WriteLine("mars config path: {0}", configPath);
            var file = File.ReadAllText(configPath);
            var simConfig = SimulationConfig.Deserialize(file);

            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        var simulation = application.Resolve<ISimulation>();

        var state = simulation.StartSimulation();

        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        application.Dispose();
    }
}