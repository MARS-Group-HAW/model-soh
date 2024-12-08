using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel.SemiTruck.Model;
using SOHModel.SemiTruck.Scheduling;

namespace SOHTravellingBox;

internal static class Program
{
    public static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("EN-US");
        LoggerFactory.SetLogLevel(LogLevel.Warning);

        var description = new ModelDescription();

        // Add only the necessary layers for car simulation
        // description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });
        description.AddLayer<SemiTruckLayer>();
        description.AddLayer<SemiTruckSchedulerLayer>();
        description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
        // Add car-related entities
        description.AddEntity<SemiTruck>();

        
        

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
            // var simConfig = CreateDefaultConfig();
            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        
        
        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();

        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
        application.Dispose();
    }

    private static SimulationConfig CreateDefaultConfig()
    {
        Console.WriteLine("Creating default configuration...");
        var startPoint = DateTime.Parse("2021-10-11T06:00:00");
        var endPoint = DateTime.Parse("2021-10-11T11:00:00");
        var config = new SimulationConfig
        {
            SimulationIdentifier = "autobahn_simulation",
            Globals =
            {
                StartPoint = startPoint,
                EndPoint = endPoint,
                DeltaTUnit = TimeSpanUnit.Seconds,
                ShowConsoleProgress = true,
                DeltaT = 1,
                OutputTarget = OutputTargetType.GeoJsonFile,
                // simulation output formats
            },
            AgentMappings =
            {
                new AgentMapping()
                {
                    Name = "CarDriver",
                    InstanceCount = 10,
                    OutputKind = OutputKind.Full,
                    Outputs = new List<Output>
                        {
                            new Output
                            {
                    OutputConfiguration = new OutputConfiguration()
                    {
                        TripsDiscriminatorFields = new string[]
                        {
                            "StableId",
                            "StartPosition",
                            "EndPosition",
                            "DistanceTraveled", 
                            "Duration"
                        }
                    }
                    }
                            },
                    IndividualMapping = new List<IndividualMapping>()
                    {
                        new IndividualMapping() { Name = "startLat", Value = 48.23607 },
                        new IndividualMapping { Name = "startLon", Value = 11.59965 },
                        new IndividualMapping { Name = "destLat", Value = 52.684 },
                        new IndividualMapping { Name = "destLon", Value = 13.2158 },
                        new IndividualMapping { Name = "ResultTrajectoryEnabled", Value = true },
                        new IndividualMapping { Name = "driveMode", Value = 1 }
                    }
                    
                }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = "CarLayer",
                    File = "resources/autobahn_and_bundesstreet.geojson",
                    InputConfiguration = new InputConfiguration()
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType>
                        {
                            SpatialModalityType.CarDriving
                        },
                    }
                    
                }
            },
            EntityMappings =
            {
                new EntityMapping()
                {
                    Name = "Car",
                    File = "resources/car.csv"
                }
                 
            }
            
            // layer configuration
            // agent configuration
            
        };
        return config;
    }
}


// {
// "parameter": "startLat",
// "value": 48.23607
// },
// {
//     "parameter": "startLon",
//     "value": 11.59965
// },
// {
//     "parameter": "destLat",
//     "value": 52.684
// },
// {
//     "parameter": "destLon",
//     "value": 13.2158
// },
// {
//     "parameter": "driveMode",
//     "value": 1
// },
