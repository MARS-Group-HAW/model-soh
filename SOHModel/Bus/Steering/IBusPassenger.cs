using SOHModel.Bus.Station;

namespace SOHModel.Bus.Steering;

/// <summary>
///     Provides access to the bus stations, which is necessary to use the bus.
/// </summary>
public interface IBusPassenger
{
    /// <summary>
    ///     Provides access to all bus stations.
    /// </summary>
    IBusStationLayer BusStationLayer { get; }
}