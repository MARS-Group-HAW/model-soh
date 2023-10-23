using SOHBusModel.Route;
using SOHBusModel.Station;

namespace SOHBusModel.Model;

public interface IBusRouteLayer
{
    /// <summary>
    ///     Provides access to all stations
    /// </summary>
    public BusStationLayer BusStationLayer { get; }

    /// <summary>
    ///     Tries to find a route for given line.
    /// </summary>
    /// <param name="line">Identifies the bus route</param>
    /// <param name="busRoute">Returns the searched bus route.</param>
    /// <returns>True, if a train route for given identifier exists, false otherwise</returns>
    bool TryGetRoute(string line, out BusRoute busRoute);
}