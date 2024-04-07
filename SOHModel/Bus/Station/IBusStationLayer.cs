using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Model;

namespace SOHModel.Bus.Station;

/// <summary>
///     The <see cref="IBusStationLayer" /> capsules the access to all <see cref="BusStation" />s.
/// </summary>
public interface IBusStationLayer : IModalLayer, IVectorLayer
{
    /// <summary>
    ///     Tries to find the nearest <see cref="BusStation" /> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="predicate">Optional predicate to limit the result</param>
    /// <returns>The corresponding <see cref="BusStation" /> if one is found, null otherwise.</returns>
    BusStation? Nearest(Position position, Func<BusStation, bool> predicate = null);
}