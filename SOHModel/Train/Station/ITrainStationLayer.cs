using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Model;

namespace SOHModel.Train.Station;

/// <summary>
///     The <see cref="ITrainStationLayer" /> capsules the access to all <see cref="TrainStation" />s.
/// </summary>
public interface ITrainStationLayer : IModalLayer, IVectorLayer
{
    /// <summary>
    ///     Tries to find the nearest <see cref="TrainStation" /> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="predicate">Optional predicate to limit the result</param>
    /// <returns>The corresponding <see cref="TrainStation" /> if one is found, null otherwise.</returns>
    TrainStation Nearest(Position? position, Func<TrainStation, bool> predicate = null);
}