using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Model;

namespace SOHModel.Tram.Station;

public interface ITramStationLayer: IModalLayer, IVectorLayer
{
    /// <summary>
    ///     Tries to find the nearest <see cref="TramStation" /> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="predicate">Optional predicate to limit the result</param>
    /// <returns>The corresponding <see cref="TramStation" /> if one is found, null otherwise.</returns>
    TramStation Nearest(Position? position, Func<TramStation, bool> predicate = null);
}