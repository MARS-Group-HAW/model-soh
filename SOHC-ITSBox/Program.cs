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
        LoggerFactory.SetLogLevel(LogLevel.Warning); 
        
        //Console.WriteLine("Hello World!");
        
        var description = new ModelDescription();

        description.AddLayer<TrafficLightLayer>();
        description.AddLayer<CarLayer>();
        description.AddAgent<CarDriver, CarLayer>();
        //description.AddLayer<CarDriverSchedulerLayer>();
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

        /*
        var startTime = DateTime.Parse("2025-01-01T00:00:00");
        var start = Position.CreateGeoPosition(9.937944800, 53.547771400);
        var goal = Position.CreateGeoPosition(9.948982100, 53.552160201);
        var config = new SimulationConfig
        {
            Globals =
            {
                StartPoint = startTime,
                EndPoint = startTime + TimeSpan.FromHours(1),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv,
                CsvOptions = { OutputPath = GetType().Name }
                //GeoJsonOptions = {OutputPath = GetType().Name }
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(CarLayer),
                    Value = new SpatialGraphEnvironment(DriveGraphAltonaAltstadt)
                },
                new LayerMapping
                {
                    Name = nameof(TrafficLightLayer),
                    File = ResourcesConstants.TrafficLightsAltona
                }
            },
            AgentMappings = new List<AgentMapping>
            {
                new()
                {
                    Name = nameof(CarDriver),
                    InstanceCount = 1,
                    IndividualMapping = new List<IndividualMapping>
                    {
                        new() { Name = "driveMode", Value = 3 },
                        new() { Name = "startLat", Value = start.Latitude },
                        new() { Name = "startLon", Value = start.Longitude },
                        new() { Name = "destLat", Value = goal.Latitude },
                        new() { Name = "destLon", Value = goal.Longitude }
                    }
                }
            },
            EntityMappings = new List<EntityMapping>
            {
                new()
                {
                    Name = nameof(Car),
                    File = "resources/car.csv"
                }
            }
        };
        */
        
        /*
        var starter = SimulationStarter.Start(description, config);
        var workflowState = starter.Run();



        var table = CsvReader.MapData(Path.Combine(GetType().Name, nameof(CarDriver) + ".csv"));
        var firstRow = table.Select("Tick = '0'")[0];
        var posAfterFirstTick =
            Position.CreateGeoPosition(firstRow["Longitude"].Value<double>(), firstRow["Latitude"].Value<double>());
        var lastRow = table.Select("Tick = '276'")[0];
        var reachedGoal =
            Position.CreateGeoPosition(lastRow["Longitude"].Value<double>(), lastRow["Latitude"].Value<double>());
        */
    }
}