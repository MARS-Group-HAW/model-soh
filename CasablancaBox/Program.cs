using System.Diagnostics;
using System.Globalization;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;

// Graph
using SOHModel.Domain.Graph;

// Bus
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;

// Train
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;

// Travelers (multimodal)
using SOHModel.Multimodal.Model;

// StreetLayer, HumanTravelerLayer (if you use it)

namespace CasablancaBox
{
    internal static class Program
    {
        private enum Mode { Tram, Train, Bus, Walk }

        public static int Main(string[] args)
        {
            // predictable parsing of decimals (WKT etc.)
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            // defaults
            var mode = Mode.Bus;
            string? configPath = null;
            var logLevel = LogLevel.Info;

            // simple args
            foreach (var raw in args)
            {
                var a = raw.Trim();
                if (a.Equals("tram", StringComparison.OrdinalIgnoreCase)) mode = Mode.Tram;
                else if (a.Equals("train", StringComparison.OrdinalIgnoreCase)) mode = Mode.Train;
                else if (a.Equals("bus", StringComparison.OrdinalIgnoreCase)) mode = Mode.Bus;
                else if (a.Equals("walk", StringComparison.OrdinalIgnoreCase) || a.Equals("walking", StringComparison.OrdinalIgnoreCase)) mode = Mode.Walk;
                else if (a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("-m=", StringComparison.OrdinalIgnoreCase))
                    mode = ParseMode(ValueOf(a));
                else if (a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("-c=", StringComparison.OrdinalIgnoreCase))
                    configPath = ValueOf(a);
                else if (a.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                    logLevel = ParseLogLevel(ValueOf(a));
            }

            LoggerFactory.SetLogLevel(logLevel);

            // pick default config if not provided
            configPath ??= DefaultConfigFor(mode);

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"[FATAL] Config not found: {Path.GetFullPath(configPath)}");
                Console.Error.WriteLine("Usage: dotnet run [bus|tram|train|walk] [--config=path.json] [--log=Info|Debug|Warn|Error|Trace]");
                return 2;
            }

            // Build model for selected mode
            var description = BuildModel(mode);

            // Load config JSON
            SimulationConfig simConfig;
            try
            {
                simConfig = SimulationConfig.Deserialize(File.ReadAllText(configPath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL] Failed to parse '{configPath}': {ex.Message}");
                return 3;
            }

            // Build & validate
            ISimulationContainer app;
            try
            {
                app = SimulationStarter.BuildApplication(description, simConfig);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL] Boot/validation failed: {ex}");
                return 4;
            }

            // Run
            var sim = app.Resolve<ISimulation>();
            Console.WriteLine($"[INFO] Starting simulation. Mode={mode} Config={Path.GetFileName(configPath)} Log={logLevel}");
            var sw = Stopwatch.StartNew();
            var state = sim.StartSimulation();
            sw.Stop();
            Console.WriteLine($"[DONE] Iterations={state.Iterations} Elapsed={sw.Elapsed}");

            app.Dispose();
            return 0;
        }

        // -------- model per mode --------

        private static ModelDescription BuildModel(Mode mode)
        {
            var d = new ModelDescription();

            d.AddLayer<SpatialGraphMediatorLayer>();

            switch (mode)
            {
                case Mode.Bus:
                    // Bus stack
                    d.AddLayer<BusLayer>();
                    d.AddLayer<BusSchedulerLayer>();
                    d.AddLayer<BusStationLayer>();
                    d.AddLayer<BusRouteLayer>(new[] { typeof(IBusRouteLayer) });
                    d.AddAgent<BusDriver, BusLayer>();
                    d.AddEntity<Bus>();

                    // Travelers that can do walking + bus
                    d.AddLayer<PassengerTravelerLayer>();
                    d.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>("PassengerTravelerSchedulerLayer");
                    d.AddAgent<PassengerTraveler, PassengerTravelerLayer>();
                    break;

                case Mode.Tram:
                    // Tram stack (your TramDriver routes on Ferry or TrainDriving, per your wiring)
                    d.AddLayer<TrainLayer>();
                    d.AddLayer<TrainSchedulerLayer>();
                    d.AddLayer<TrainStationLayer>();
                    d.AddLayer<TrainRouteLayer>(new[] { typeof(ITrainRouteLayer) });
                    d.AddAgent<TrainDriver, TrainLayer>();
                    d.AddEntity<Train>();

                    d.AddLayer<PassengerTravelerLayer>();
                    d.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>("PassengerTravelerSchedulerLayer");
                    d.AddAgent<PassengerTraveler, PassengerTravelerLayer>();
                    break;

                case Mode.Train:
                    d.AddLayer<TrainLayer>();
                    d.AddLayer<TrainSchedulerLayer>();
                    d.AddLayer<TrainStationLayer>();
                    d.AddLayer<TrainRouteLayer>(new[] { typeof(ITrainRouteLayer) });
                    d.AddAgent<TrainDriver, TrainLayer>();
                    d.AddEntity<Train>();

                    d.AddLayer<PassengerTravelerLayer>();
                    d.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>("PassengerTravelerSchedulerLayer");
                    d.AddAgent<PassengerTraveler, PassengerTravelerLayer>();
                    break;

                case Mode.Walk:
                    // Minimal walking-only scenario
                    // If your HumanTraveler pipeline is used:
                    d.AddLayer<HumanTravelerLayer>();
                    d.AddLayer<AgentSchedulerLayer<HumanTraveler, HumanTravelerLayer>>("HumanTravelerSchedulerLayer");
                    d.AddAgent<HumanTraveler, HumanTravelerLayer>();

                    // Optional: if your walking spawner needs it
                    d.AddLayer<StreetLayer>();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            return d;
        }

        // -------- helpers --------

        private static string DefaultConfigFor(Mode mode) => mode switch
        {
            Mode.Bus  => "config_bus.json",
            Mode.Tram => "config_tram.json",
            Mode.Train=> "config_tram.json",
            Mode.Walk => "config_walking.json",
            _ => "config_bus.json"
        };

        private static string ValueOf(string arg)
        {
            var idx = arg.IndexOf('=');
            return idx >= 0 ? arg[(idx + 1)..] : string.Empty;
        }

        private static Mode ParseMode(string v) => v.ToLowerInvariant() switch
        {
            "bus" => Mode.Bus,
            "tram" => Mode.Tram,
            "train" => Mode.Train,
            "walk" or "walking" => Mode.Walk,
            _ => throw new ArgumentOutOfRangeException(nameof(v), $"Unknown mode '{v}'. Use bus|tram|train|walk.")
        };

        private static LogLevel ParseLogLevel(string value) => value.ToLowerInvariant() switch
        {
            "info"  => LogLevel.Info,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }
}
