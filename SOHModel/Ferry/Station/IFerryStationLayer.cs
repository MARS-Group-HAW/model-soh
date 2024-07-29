using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Model;

namespace SOHModel.Ferry.Station;

/// <summary>
///     The <see cref="IFerryStationLayer" /> capsules the access to all <see cref="FerryStation" />s.
/// </summary>
public interface IFerryStationLayer : IModalLayer, IVectorLayer
{
    /// <summary>
    ///     Tries to find the nearest <see cref="FerryStation" /> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="predicate">Optional predicate to limit the result</param>
    /// <returns>The corresponding <see cref="FerryStation" /> if one is found, null otherwise.</returns>
    FerryStation Nearest(Position position, Func<FerryStation, bool>? predicate = null);

    /// <summary>
    ///     Executes a point query at the specified position to get all limited order of features
    ///     inside the radius in kilometre. Optional it can be filtered by a predicate over attribute table
    /// </summary>
    /// <param name="position">The outgoing position (lat, lon) from which to start the query.</param>
    /// <param name="radius">The exploration radius in meter or -1 for infinite.</param>
    /// <param name="count">The maximum amount of features to query or -1 for infinite.</param>
    /// <param name="predicate">The optional predicate to filter out specific features</param>
    /// <returns>A collection with distance based order of the explored features.</returns>
    public IEnumerable<FerryStation> Explore(double[] position, double radius = -1,
        Func<FerryStation, bool>? predicate = null);
}