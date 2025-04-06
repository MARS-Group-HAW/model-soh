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
using NetTopologySuite.GeometriesGraph;
using ServiceStack;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents the layer responsible for managing SemiTruck-related agents and their environment.
    /// Handles initialization of the environment and spawning of agents.
    /// </summary>
    public class SemiTruckLayer : AbstractLayer, ISemiTruckLayer, ISpatialGraphLayer, ISteppedActiveLayer 
    {
        
        public List<RoadClosure> ScheduledClosures { get; private set; } = new List<RoadClosure>();
        
        

        
        private DateTime _simulationTime = DateTime.MinValue;
        
        private TimeSpan _tickDuration = TimeSpan.MinValue; // Default
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
        public List<ISpatialEdge> RemovedEdges { get; private set; } = new List<ISpatialEdge>();

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
            string filePath = Path.Combine(AppContext.BaseDirectory, "resources", "road_closures.csv");

            LoadRoadClosures(filePath);
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
                        IsBiDirectedImport = layerInitData.LayerInitConfig.InputConfiguration?.IsBiDirectedImport ?? false //If Bidirectional Value is not in config it will be false
                    }
                });
                
            }
            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            _simulationTime = layerInitData.SimulationStartPointDateTime ?? DateTime.MinValue;
            _tickDuration = layerInitData.OneTickTimeSpan ?? TimeSpan.FromSeconds(1);
        
            // Create and register objects of type MyAgentType.
            var agents = agentManager.Spawn<SemiTruckDriver, SemiTruckLayer>(
                dependencies: new List<IModelObject> { this, Environment });
            
            
            // Otherwise only create them but do not registering 
            // to trigger their Tick() method. 
            agentManager.Create<SemiTruckDriver>().ToList();
            
            // Add the spawned agents to the Driver dictionary for tracking
            Driver.AddRange(agents.ToDictionary(agent => agent.ID, agent => (IAgent)agent));

            
            return true;
        }
        /// <summary>
        /// Loads road closure information from a specified CSV file and populates the <see cref="ScheduledClosures"/> list.
        /// Each line in the file is expected to contain an edge ID and corresponding start and end times for the closure.
        /// </summary>
        /// <param name="model-soh/SOHLogisticsBox/resources/road_closures.csv">The full path to the CSV file containing road closure data.</param>

        public void LoadRoadClosures(string filePath)
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

                ScheduledClosures.Add(new RoadClosure
                {
                    EdgeId = edgeId,
                    StartTime = startTime,
                    EndTime = endTime
                });
            }
        }


        private bool edgeRemoved = false;  // Ensure we remove it only once

        
        /// <summary>
        /// Advances the simulation time and applies road closures or reopens roads based on the current simulation tick.
        /// Removes or restores edges in the environment according to the schedule defined in <see cref="ScheduledClosures"/>.
        /// </summary>

        public void Tick()
        {
            // Advance simulation time
            _simulationTime += _tickDuration;

            foreach (var closure in ScheduledClosures)
            {
                if (!closure.IsRemoved && _simulationTime >= closure.StartTime && _simulationTime < closure.EndTime)
                {
                    // Remove the edge
                    if (Environment.Edges.TryGetValue(closure.EdgeId, out var edge))
                    {
                        RemovedEdges.Add(edge);
                        Environment.Edges.Remove(closure.EdgeId);
                        closure.IsRemoved = true;
                        Console.WriteLine($"Edge {closure.EdgeId} removed at {_simulationTime}.");
                    }
                }
                else if (closure.IsRemoved && _simulationTime >= closure.EndTime)
                {
                    // Restore the edge
                    if (RemovedEdges.FirstOrDefault(e => e.GetId().Equals(closure.EdgeId)) is ISpatialEdge restoredEdge)
                    {
                        Environment.Edges.Add(closure.EdgeId, restoredEdge);
                        RemovedEdges.Remove(restoredEdge);
                        closure.IsRemoved = false;
                        Console.WriteLine($"Edge {closure.EdgeId} restored at {_simulationTime}.");
                    }
                }
            }
        }

        



        public void PreTick()
        {
            
        }

        public void PostTick()
        {
            
        }
        public class RoadClosure
        {
            public int EdgeId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool IsRemoved { get; set; } = false; // Track whether it's removed
        }

    }
}
