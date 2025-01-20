using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Multimodal.Model;

public class DockWorkerLayer : AbstractMultimodalLayer
{
    public IDictionary<Guid, DockWorker> Agents { get; set; } = new Dictionary<Guid, DockWorker>();

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var agentMapping =
            layerInitData.AgentInitConfigs.FirstOrDefault(mapping =>
                mapping.ModelType.MetaType == typeof(DockWorker));

        if (agentMapping != null && registerAgentHandle != null && unregisterAgent != null)
        {
            Agents = AgentManager.SpawnAgents<DockWorker>(agentMapping,
                registerAgentHandle, unregisterAgent, [this]);
        }

        return true;
    }
}