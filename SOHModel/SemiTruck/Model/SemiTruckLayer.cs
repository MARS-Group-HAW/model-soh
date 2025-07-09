using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using ServiceStack.Text;
using SOHModel.Domain.Graph;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NetTopologySuite.GeometriesGraph;
using ServiceStack;
using SOHModel.SemiTruck.Common;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents the layer responsible for managing SemiTruck-related agents and their environment.
    /// Handles initialization of the environment and spawning of agents.
    /// </summary>
    public class SemiTruckLayer : AbstractLayer, ISemiTruckLayer, ISpatialGraphLayer, ISteppedActiveLayer
    {
        // Stores closures defined by spatial edge IDs
        public List<ScheduledRoadClosureByID> ScheduledClosuresByID { get; private set; } =
            new List<ScheduledRoadClosureByID>();

        // Stores closures defined by coordinate paths
        public List<ScheduledRoadClosureByCoordinates> ScheduledClosuresByCoordinates = new();

        // Simulation state: current simulation time
        private DateTime _simulationTime = DateTime.MinValue;

        // Duration of each simulation tick
        public TimeSpan _tickDuration = TimeSpan.MinValue; // Default


        /// <summary>
        /// If this flag is true, Trucks will be notified if an edge on their route is removed
        /// If this flag is false they instead rely on a lookahead distance (5km) to check if an upcoming edge is removed
        /// </summary>
        public readonly bool notifyTrucks = true;
        
        /// <summary>
        /// This variable defines the amount of trucks
        /// </summary>
        public int amountOfTrucks { get; set; }

        /// <summary>
        /// The default modal choice for SemiTruckLayer is CarDriving.
        /// </summary>
        public ModalChoice ModalChoice => ModalChoice.CarDriving;

        /// <summary>
        /// The spatial graph environment where SemiTruck agents operate.
        /// </summary>
        public ISpatialGraphEnvironment Environment { get; set; }


        /// <summary>
        /// This List contains all removed edges
        /// </summary>
        public List<ISpatialEdge> RemovedEdges { get; private set; } = new();


        private readonly Dictionary<ISpatialEdge, HashSet<SemiTruckDriver>> _edgeToTrucks = new();

        /// <summary>
        /// Dictionary to hold all SemiTruck drivers, mapped by their unique identifiers.
        /// </summary>
        public IDictionary<Guid, IAgent> Driver { get; private set; } = new Dictionary<Guid, IAgent>();

        /// <summary>
        /// Initializes the SemiTruckLayer, setting up the environment and spawning agents.
        /// </summary>
        /// <param name="layerInitData">Data required for initializing the layer.</param>
        /// <param name="registerAgentHandle">Delegate to register agents.</param>
        /// <param name="unregisterAgent">Optional delegate to unregister agents.</param>
        /// <returns>True if initialization is successful; otherwise, false.</returns>
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
            UnregisterAgent? unregisterAgent = null)
        {
            // Call the base layer initialization
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            string filePathID = Path.Combine(AppContext.BaseDirectory, "resources", "road_closures_by_ID.csv");
            LoadRoadClosuresByID(filePathID);
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            string filePathCoordinates =
                Path.Combine(AppContext.BaseDirectory, "resources", "road_closures_by_Coordinates.csv");
            LoadRoadClosuresByCoordinates(filePathCoordinates);
            // Attempt to initialize the environment from Mapping.Value or file
            if (Mapping.Value is ISpatialGraphEnvironment input)
            {
                Environment = input;
            }
            else if (!string.IsNullOrEmpty(Mapping.File))
            {
                Environment = new SpatialGraphEnvironment(new Input
                {
                    File = layerInitData.LayerInitConfig.File,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = layerInitData.LayerInitConfig.InputConfiguration?.IsBiDirectedImport ??
                                             false //If Bidirectional Value is not in config it will be false
                    }
                });
            }

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            _simulationTime = layerInitData.SimulationStartPointDateTime ?? DateTime.MinValue;
            _tickDuration = layerInitData.OneTickTimeSpan ?? TimeSpan.FromSeconds(1);

            // Create and register objects of type MyAgentType.
            var agentList = agentManager
                .Spawn<SemiTruckDriver, SemiTruckLayer>(dependencies: new List<IModelObject> { this, Environment })
                .ToList(); 

            IEnumerable<SemiTruckDriver> agents = agentList; 

            amountOfTrucks = agentList.Count;

            // Otherwise only create them but do not registering 
            // to trigger their Tick() method. 
            // agentManager.Create<SemiTruckDriver>().ToList();
            
            
            // Add the spawned agents to the Driver dictionary for tracking
            Driver.AddRange(
                agents
                    .ToDictionary(agent => agent.ID, agent => (IAgent)agent)
            );


            return true;
        }

        /// <summary>
        /// Loads road closure information from a specified CSV file and populates the <see cref="ScheduledClosuresByID"/> list.
        /// Each line in the file is expected to contain an edge ID and corresponding start and end times for the closure.
        /// </summary>
        /// <param name="model-soh/SOHLogisticsBox/resources/road_closures_by_ID.csv">The full path to the CSV file containing road closure data.</param>
        public void LoadRoadClosuresByID(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Road closure file not found!");
                return;
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                int edgeId = int.Parse(parts[0]);
                DateTime startTime = DateTime.Parse(parts[1]);
                DateTime endTime = DateTime.Parse(parts[2]);

                ScheduledClosuresByID.Add(new ScheduledRoadClosureByID
                {
                    EdgeId = edgeId,
                    StartTime = startTime,
                    EndTime = endTime
                });
            }
        }

        /// <summary>
        /// Loads road closure information from a specified CSV file and populates the <see cref="ScheduledRoadClosureByCoordinates"/> list.
        /// Each line in the file is expected to contain a Coordinate and corresponding start and end times for the closure.
        /// </summary>
        /// <param name="model-soh/SOHLogisticsBox/resources/road_closures_by_Coordinates.csv">The full path to the CSV file containing road closure data.</param>
        public void LoadRoadClosuresByCoordinates(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Road closure file not found!");
                return;
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 6) continue;
                double startLat = double.Parse(parts[0]);
                double startLon = double.Parse(parts[1]);
                double endLat = double.Parse(parts[2]);
                double endLon = double.Parse(parts[3]);

                DateTime startTime = DateTime.Parse(parts[4]);
                DateTime endTime = DateTime.Parse(parts[5]);

                var coordinates = new List<Coordinate>
                {
                    new(startLon, startLat),
                    new(endLon, endLat)
                };
                var block = new SemiTruckLayer.ScheduledRoadClosureByCoordinates(
                    id: Guid.NewGuid().ToString(),
                    startTime: startTime,
                    endTime: endTime,
                    coordinates: coordinates
                );
                ScheduledClosuresByCoordinates.Add(block);
            }
        }


        private bool edgeRemoved = false; // Ensure we remove it only once


        /// <summary>
        /// Advances the simulation time and applies road closures or reopens roads based on the current simulation tick.
        /// Removes or restores edges in the environment according to the schedule defined in <see cref="ScheduledClosuresByID"/>.
        /// </summary>
        public void Tick()
        {
            _simulationTime += _tickDuration;
        }

        /// <summary>
        /// Applies scheduled road closures before each simulation tick.
        /// </summary>
        public void PreTick()
        {
            foreach (var closure in ScheduledClosuresByID)
            {
                HandleScheduledClosureByID(closure);
            }

            foreach (var roadBlock in ScheduledClosuresByCoordinates)
            {
                HandleScheduledClosureByCoordinates(roadBlock);
            }
        }

        public void PostTick()
        {
        }

        /// <summary>
        /// Handle closure or reopening of edges identified by edge ID.
        /// </summary>
        private void HandleScheduledClosureByID(ScheduledRoadClosureByID closureById)
        {
            if (!closureById.IsRemoved && _simulationTime >= closureById.StartTime &&
                _simulationTime < closureById.EndTime)
            {
                if (Environment.Edges.TryGetValue(closureById.EdgeId, out var edge))
                {
                    RemovedEdges.Add(edge);
                    Environment.Edges.Remove(closureById.EdgeId);
                    closureById.IsRemoved = true;

                    if (notifyTrucks)
                    {
                        if (_edgeToTrucks.TryGetValue(edge, out var trucks))
                        {
                            foreach (var truck in trucks.ToList()) // Kopie zur Sicherheit bei Deregistrierung
                            {
                                truck.NotifyEdgeBlocked(edge); // der Truck reagiert intern
                            }
                        }
                    }

                    // Console.WriteLine($"Edge {closureById.EdgeId} removed at {_simulationTime}.");
                }
            }
            else if (closureById.IsRemoved && _simulationTime >= closureById.EndTime)
            {
                if (RemovedEdges.FirstOrDefault(e => e.GetId().Equals(closureById.EdgeId)) is ISpatialEdge restoredEdge)
                {
                    Environment.Edges.Add(closureById.EdgeId, restoredEdge);
                    RemovedEdges.Remove(restoredEdge);
                    closureById.IsRemoved = false;
                    // Console.WriteLine($"Edge {closureById.EdgeId} restored at {_simulationTime}.");
                }
            }
        }

        /// <summary>
        /// Handle closure or reopening of edges identified by coordinates (via route).
        /// </summary>
        private void HandleScheduledClosureByCoordinates(ScheduledRoadClosureByCoordinates closureByCoordinates)
        {
            if (!closureByCoordinates.IsActive && _simulationTime >= closureByCoordinates.StartTime &&
                _simulationTime < closureByCoordinates.EndTime)
            {
                var start = closureByCoordinates.Coordinates.First();
                var end = closureByCoordinates.Coordinates.Last();
                var currentNode = Environment.NearestNode(Position.CreateGeoPosition(start.X, start.Y), outgoingModality: SpatialModalityType.CarDriving);
                var goal = Environment.NearestNode(Position.CreateGeoPosition(end.X, end.Y), incomingModality: SpatialModalityType.CarDriving);

                var route = Environment.FindShortestRoute(currentNode, goal);
                

                if (route != null && route.Stops != null)
                {
                    foreach (var stop in route.Stops)
                    {
                        var edge = stop.Edge;
                        if (edge != null && !RemovedEdges.Contains(edge))
                        {
                            RemovedEdges.Add(edge);
                            Environment.RemoveEdge(edge);
                            if (notifyTrucks)
                            {
                                if (_edgeToTrucks.TryGetValue(edge, out var trucks))
                                {
                                    foreach (var truck in trucks.ToList()) // Kopie zur Sicherheit bei Deregistrierung
                                    {
                                        truck.NotifyEdgeBlocked(edge); // der Truck reagiert intern
                                    }
                                }
                            }

                            // Console.WriteLine($"[RoadBlock] Edge removed at {_simulationTime}.");
                        }
                    }

                    closureByCoordinates.IsActive = true;
                }
            }
            else if (closureByCoordinates.IsActive && _simulationTime >= closureByCoordinates.EndTime)
            {
                var toRestore = RemovedEdges.ToList(); // Kopie zur sicheren Iteration

                foreach (var edge in toRestore)
                {
                    int edgeId = (int)edge.GetId();
                    Environment.Edges.Add(edgeId, edge);
                    RemovedEdges.Remove(edge);
                    // Console.WriteLine($"[RoadBlock] Edge restored at {_simulationTime}.");
                }

                closureByCoordinates.IsActive = false;
            }
        }

        /// <summary>
        /// Registers a truck to track all edges along its route.
        /// </summary>
        public void RegisterTruckForRoute(SemiTruckDriver truck, Route route)
        {
            foreach (var stop in route)
            {
                if (!_edgeToTrucks.TryGetValue(stop.Edge, out var trucks))
                    _edgeToTrucks[stop.Edge] = trucks = new HashSet<SemiTruckDriver>();

                trucks.Add(truck);
            }
        }

        /// <summary>
        /// Unregisters a truck from all edges of its previous route.
        /// </summary>
        public void UnregisterTruckFromRoute(SemiTruckDriver truck, Route oldRoute)
        {
            foreach (var stop in oldRoute)
            {
                if (_edgeToTrucks.TryGetValue(stop.Edge, out var trucks))
                {
                    trucks.Remove(truck);
                    if (trucks.Count == 0)
                        _edgeToTrucks.Remove(stop.Edge); // optional für sauberen Speicher
                }
            }
        }


        /// <summary>
        /// Represents a scheduled closure based on edge ID.
        /// </summary>
        public class ScheduledRoadClosureByID
        {
            public int EdgeId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool IsRemoved { get; set; } = false; // Track whether it's removed
        }

        /// <summary>
        /// Represents a scheduled closure based on coordinate-defined routes.
        /// </summary>
        public class ScheduledRoadClosureByCoordinates
        {
            public string Id { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public List<Coordinate> Coordinates { get; set; } = new();

            public ScheduledRoadClosureByCoordinates(string id, DateTime startTime, DateTime endTime,
                List<Coordinate> coordinates)
            {
                Id = id;
                StartTime = startTime;
                EndTime = endTime;
                Coordinates = coordinates ?? new List<Coordinate>();
            }

            public HashSet<int> AffectedEdgeIds { get; set; } = new();
            public bool IsActive { get; set; } = false;
        }
    }
}