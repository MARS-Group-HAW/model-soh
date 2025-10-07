using System.Collections.Concurrent;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Multimodal.Model;

public class MarketTravelerLayer : AbstractMultimodalLayer, IMarketTravelerLayer
{
    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;

    private static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();

    private readonly List<MarketTraveler> _activeTravelers = new();

    public new bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgentHandle)
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
        // --- UPDATED: Remove the agent from our tracking list before unregistering ---
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.Remove(traveler);
        }

        _unregisterAgent?.Invoke(this, agent);
    }

    public override void SetCurrentTick(long currentStep)
    {
        base.SetCurrentTick(currentStep);

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);

            // --- NEW: Once registered, add the agent to our active list for queries ---
            if (agent is MarketTraveler traveler && !_activeTravelers.Contains(traveler))
            {
                _activeTravelers.Add(traveler);
            }
        }
    }

    // --- NEW: Implementation of the spatial query method ---
    /// <summary>
    /// Finds all MarketTraveler agents within a specified radius of a given position.
    /// </summary>
    public IEnumerable<MarketTraveler> GetNearest(Position position, double radius)
    {
        if (position == null)
        {
            return Enumerable.Empty<MarketTraveler>();
        }

        // Query our active traveler list
        return _activeTravelers.Where(traveler =>
            traveler.Position != null &&
            position.DistanceInMTo(traveler.Position) <= radius
        );
    }
}