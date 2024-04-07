using Mars.Interfaces.Layers;
using SOHModel.Train.Route;
using SOHModel.Train.Station;

namespace SOHModel.Train.Model;

public interface ITrainRouteLayer : ILayer
{
    /// <summary>
    ///     Provides access to all stations
    /// </summary>
    public TrainStationLayer TrainStationLayer { get; }

    /// <summary>
    ///     Tries to find a route for given line.
    /// </summary>
    /// <param name="line">Identifies the train route</param>
    /// <param name="trainRoute">Returns the searched train route.</param>
    /// <returns>True, if a train route for given identifier exists, false otherwise</returns>
    bool TryGetRoute(string line, out TrainRoute? trainRoute);
}