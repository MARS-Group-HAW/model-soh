using Mars.Interfaces.Layers;
using SOHModel.Tram.Route;
using SOHModel.Tram.Station;
namespace SOHModel.Tram.Model;

public interface ITramRouteLayer: ILayer
{
    /// <summary>
    ///     Provides access to all stations
    /// </summary>
    public TramStationLayer TramStationLayer { get; }

    /// <summary>
    ///     Tries to find a route for given line.
    /// </summary>
    /// <param name="line">Identifies the tram route</param>
    /// <param name="tramRoute">Returns the searched tram route.</param>
    /// <returns>True, if a tram route for given identifier exists, false otherwise</returns>
    bool TryGetRoute(string line, out TramRoute? tramRoute);
}