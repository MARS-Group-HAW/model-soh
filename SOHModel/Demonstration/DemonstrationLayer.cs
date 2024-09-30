using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model.Options;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Demonstration
{
    /// <summary>
    ///     This layer implements the <see cref="AbstractMultimodalLayer" /> to provide multi-modal routing capabilities.
    /// </summary>
    public class DemonstrationLayer : AbstractMultimodalLayer
    {
        /// <summary>
        ///     Collection storing all <see cref="Police" /> agents that have been registered and spawned on the layer
        /// </summary>
        public IDictionary<Guid, Police> PoliceMap { get; set; } = new Dictionary<Guid, Police>();
        public IDictionary<Guid, PoliceChief> ChiefMap { get; set; } = new Dictionary<Guid, PoliceChief>();

        /// <summary>
        ///     Collection storing all <see cref="Demonstrator" /> agents that have been registered and spawned on the layer
        /// </summary>
        public IDictionary<Guid, Demonstrator> DemonstratorMap { get; set; } = new Dictionary<Guid, Demonstrator>();
        
        /// <summary>
        ///     Collection storing all <see cref="RadicalDemonstrator" /> agents that have been registered and spawned on the layer
        /// </summary>
        public IDictionary<Guid, RadicalDemonstrator> RadicalDemonstratorMap { get; set; } = new Dictionary<Guid, RadicalDemonstrator>();
        public ICollection<ISpatialNode> LeftPoliceRouteNodes { get; set; } = null!;
        public ICollection<ISpatialNode> RightPoliceRouteNodes { get; set; } = null!;

        /// <summary>
        ///     Initialize layer and spawn <see cref="Police" /> agents
        /// </summary>
        /// <param name="layerInitData"></param> initialization data for layer type
        /// <param name="registerAgentHandle"></param> handle for registering agents on the layer
        /// <param name="unregisterAgentHandle"></param> handle for unregistering agents on the layer
        /// <returns></returns>
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent? registerAgentHandle = null,
            UnregisterAgent? unregisterAgentHandle = null)
        {
            // call the super class's InitLayer method to register agents on the layer
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);

            // Load left and right police route nodes from geojson
            var routeEnvLeft = new SpatialGraphEnvironment(new SpatialGraphOptions()
            {
                GraphImports = [layerInitData.LayerInitConfig.Inputs[0]]
            });
            var routeEnvRight = new SpatialGraphEnvironment(new SpatialGraphOptions()
            {
                GraphImports = [layerInitData.LayerInitConfig.Inputs[1]]
            });
            
            LeftPoliceRouteNodes = routeEnvLeft.Nodes;
            RightPoliceRouteNodes = routeEnvRight.Nodes;

            // spawn agents on layer and store them in PoliceMap collection
            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            
            PoliceMap = agentManager.Spawn<Police, DemonstrationLayer>().ToDictionary(police => police.ID);
            ChiefMap = agentManager.Spawn<PoliceChief, DemonstrationLayer>().ToDictionary(chief => chief.ID);

            return PoliceMap.Count != 0;
        }
    }
}