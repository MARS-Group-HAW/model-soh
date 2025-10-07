using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.Multimodal.Model;

public interface IMarketTravelerLayer : IMultimodalLayer
{
    /// <summary>
    /// Queues an agent to be formally registered at the start of the next tick.
    /// </summary>
    void EnqueueRegister(ITickClient agent);

    /// <summary>
    /// Immediately unregisters an agent from the layer and the simulation.
    /// </summary>
    void Unregister(ITickClient agent);

    /// <summary>
    /// Finds all MarketTraveler agents within a specified radius of a given position.
    /// </summary>
    /// <param name="position">The center of the search area.</param>
    /// <param name="radius">The search radius in meters.</param>
    /// <returns>An enumeration of nearby MarketTraveler agents.</returns>
    IEnumerable<MarketTraveler> GetNearest(Position position, double radius);
}