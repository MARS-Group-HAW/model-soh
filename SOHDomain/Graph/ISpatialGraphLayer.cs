using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHDomain.Graph;

/// <summary>
///     An interface for all layer managing a <see cref="ISpatialGraphEnvironment" />
/// </summary>
public interface ISpatialGraphLayer : ILayer
{
    /// <summary>
    ///     Gets the entity environment represented as a graph.
    /// </summary>
    public ISpatialGraphEnvironment Environment { get; }

    public ISimulationContext Context { get; }
}