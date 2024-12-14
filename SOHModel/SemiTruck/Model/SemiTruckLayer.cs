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
using ServiceStack.Text;
using SOHModel.Domain.Graph;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents the layer responsible for managing SemiTruck-related agents and their environment.
    /// Handles initialization of the environment and spawning of agents.
    /// </summary>
    public class SemiTruckLayer : AbstractLayer, ISemiTruckLayer, ISpatialGraphLayer
    {
        /// <summary>
        /// The default modal choice for SemiTruckLayer is CarDriving.
        /// </summary>
        public ModalChoice ModalChoice => ModalChoice.CarDriving;

        /// <summary>
        /// The spatial graph environment where SemiTruck agents operate.
        /// </summary>
        public ISpatialGraphEnvironment Environment { get; set; }

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
            
            // Attempt to initialize the environment from Mapping.Value or file
            if (Mapping.Value is ISpatialGraphEnvironment input)
            {
                Environment = input;
            }
            else if (!string.IsNullOrEmpty(Mapping.File))
            {
                // Load the environment from a specified file
                    Environment = new SpatialGraphEnvironment(layerInitData.LayerInitConfig.File);
            }
            
            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        
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
    }
}
