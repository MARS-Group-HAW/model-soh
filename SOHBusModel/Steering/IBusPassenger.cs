using SOHBusModel.Station;

namespace SOHBusModel.Steering;

/// <summary>
///     Provides access to the bus stops, which is necessary to use the bus.
/// </summary>
public interface IBusPassenger
{
    /// <summary>
    ///     Provides access to all bus stops.
    /// </summary>
    IBusStationLayer BusStationLayer { get; }
}