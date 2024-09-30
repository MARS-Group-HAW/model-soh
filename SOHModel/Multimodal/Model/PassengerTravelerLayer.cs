using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Multimodal.Model;

/// <summary>
///     A <code>IMultimodalLayer</code> that spawns <code>PassengerTraveler</code> agents
/// </summary>
public class PassengerTravelerLayer : AbstractMultimodalLayer
{
    public PassengerTravelerLayer()
    {
        Agents = new Dictionary<Guid, PassengerTraveler>();
    }

    public IDictionary<Guid, PassengerTraveler> Agents { get; set; }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        var initiated = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var agentMapping =
            layerInitData.AgentInitConfigs.FirstOrDefault(mapping =>
                mapping.ModelType.MetaType == typeof(PassengerTraveler));

        if (agentMapping != null)
            Agents = AgentManager.SpawnAgents<PassengerTraveler>(agentMapping, registerAgentHandle, unregisterAgent,
                new List<ILayer> { this });

        return initiated;
    }
}