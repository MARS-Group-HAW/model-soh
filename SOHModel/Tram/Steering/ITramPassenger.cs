using SOHModel.Tram.Station;

namespace SOHModel.Tram.Steering;
/// <summary>
///     Provides access to the tram stations, which is necessary to use the train.
/// </summary>
public interface ITramPassenger
{
    ITramStationLayer TramStationLayer { get; }
}