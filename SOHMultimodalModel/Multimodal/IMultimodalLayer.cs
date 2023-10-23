using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Layers;

namespace SOHMultimodalModel.Multimodal;

/// <summary>
///     Provides access to relevant resources within a multimodal context.
/// </summary>
public interface IMultimodalLayer : ILayer, IMultimodalRouteFinder
{
    /// <summary>
    ///     Holds the simulation specific time and progress context.
    /// </summary>
    ISimulationContext Context { get; }

    /// <summary>
    ///     Gets the unregister handle to deactivate an <see cref="ITickClient" />s at the runtime system to prevent
    ///     any execution and stop the observation.
    /// </summary>
    UnregisterAgent UnregisterAgent { get; }
}