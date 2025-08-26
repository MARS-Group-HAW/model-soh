using System.Collections.Concurrent;
using Mars.Components.Layers;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Domain.Model;

public class MarketLayer : VectorLayer, ILayer
{
    private long _currentTick;

    public static MarketLayer Current { get; private set; }

    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;

    private static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();

    public ISimulationContext Context { get; private set; }

    private readonly List<MarketStall> _stalls = new();
    public IReadOnlyList<MarketStall> Stalls => _stalls;

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle, UnregisterAgent unregisterAgentHandle)
    {
        Context = layerInitData?.Context;
        Current = this;

        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;

        return base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);
    }

    public new void DisposeLayer()
    {
    }

    public long GetCurrentTick() => _currentTick;

    public override void SetCurrentTick(long currentStep)
    {
        _currentTick = currentStep;
        base.SetCurrentTick(currentStep);

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);
        }
    }

    public void EnqueueRegister(ITickClient agent)
    {
        _registerAgent?.Invoke(this, agent);
    }

    public void Unregister(ITickClient agent)
    {
        _unregisterAgent?.Invoke(this, agent);
    }

    // TODO: für die Modelle interagierbar, nicht durchlaufbar machen
    
    public void AddStall(MarketStall stall)
    {
        if (stall == null) return;
        _stalls.Add(stall);
    }

    public MarketStall AddStall(Position position, MarketStallType type, string name)
    {
        var stall = new MarketStall();
        stall.Init(position, type, name);
        _stalls.Add(stall);
        return stall;
    }

    public MarketStall? FindNearestStall(Position pos)
    {
        if (_stalls.Count == 0 || pos == null) return null;
        MarketStall? best = null;
        double bestDist = double.MaxValue;

        foreach (var s in _stalls)
        {
            var d = pos.DistanceInMTo(s.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }
}