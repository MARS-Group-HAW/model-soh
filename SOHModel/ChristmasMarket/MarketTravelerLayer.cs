using System.Collections.Concurrent;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Multimodal.Model;

public class MarketTravelerLayer : AbstractMultimodalLayer, IMarketTravelerLayer
{
    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;

    private static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();

    public new bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle, UnregisterAgent unregisterAgentHandle)
    {
        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;
        return base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);
    }

    public void EnqueueRegister(ITickClient agent)
    {
        if (agent != null)
            PendingRegistrations.Enqueue(agent);
    }

    public void Unregister(ITickClient agent)
    {
        _unregisterAgent?.Invoke(this, agent);
    }

    public override void SetCurrentTick(long currentStep)
    {
        base.SetCurrentTick(currentStep);

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);
        }
    }
}
