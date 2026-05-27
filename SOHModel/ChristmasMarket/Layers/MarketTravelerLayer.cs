using System.Collections.Concurrent;
using Mars.Components.Services;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.ChristmasMarket.Layers;

/// <summary>
/// A specialized multimodal layer responsible for managing MarketTraveler agents.
/// </summary>
public class MarketTravelerLayer : AbstractMultimodalLayer, IMarketTravelerLayer
{
    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;
 
    protected static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();
    protected readonly List<MarketTraveler> _activeTravelers = new();

    /// <summary>
    /// Gets the unregister handle to deactivate agents.
    /// </summary>
    public UnregisterAgent? UnregisterAgent => _unregisterAgent;

    /// <summary>
    /// Initializes the layer.
    /// </summary>
    /// <param name="layerInitData">Initialization data provided by the simulation.</param>
    /// <param name="registerAgentHandle">The delegate to register agents.</param>
    /// <param name="unregisterAgentHandle">The delegate to unregister agents.</param>
    /// <returns>True when the initialization is successful.</returns>
    public new virtual bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgentHandle)
    {
        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;
        var result = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);

        foreach (var config in layerInitData.AgentInitConfigs)
        {
            AgentManager.SpawnAgents(config, registerAgentHandle, unregisterAgentHandle, [this]);
        }

        return result;
    }

    /// <summary>
    /// Queues an agent to be registered with the simulation at the start of the next tick.
    /// </summary>
    /// <param name="agent">The agent to be registered.</param>
    public void EnqueueRegister(ITickClient agent)
    {
        if (agent != null)
            PendingRegistrations.Enqueue(agent);
    }

    /// <summary>
    /// Unregisters an agent from the simulation and removes it from the internal tracking list.
    /// </summary>
    /// <param name="agent">The agent to be unregistered.</param>
    public virtual void Unregister(ITickClient agent)
    {
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.Remove(traveler);
        }

        _unregisterAgent?.Invoke(this, agent);
    }

    /// <summary>
    /// Advances the simulation time for this layer by one step. At the beginning of each tick,
    /// it processes any pending agent registrations.
    /// </summary>
    /// <param name="currentStep">The current simulation tick.</param>
    public override void SetCurrentTick(long currentStep)
    {
        base.SetCurrentTick(currentStep);

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);

            if (agent is MarketTraveler traveler && !_activeTravelers.Contains(traveler))
            {
                _activeTravelers.Add(traveler);
            }
        }
    }

    /// <summary>
    /// Finds all MarketTraveler agents within a radius of a given position.
    /// </summary>
    /// <param name="position">The center of the search area.</param>
    /// <param name="radius">The search radius in meters.</param>
    /// <returns>An enumeration of nearby MarketTraveler agents.</returns>
    public virtual IEnumerable<MarketTraveler> GetNearest(Position position, double radius)
    {
        if (position == null)
        {
            return Enumerable.Empty<MarketTraveler>();
        }

        return _activeTravelers.Where(traveler =>
            traveler.Position != null &&
            position.DistanceInMTo(traveler.Position) <= radius
        );
    }
}