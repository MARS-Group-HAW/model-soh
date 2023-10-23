using SOHFerryModel.Station;

namespace SOHFerryModel.Steering;

/// <summary>
///     Provides access to the ferry stations, which is necessary to use the ferry.
/// </summary>
public interface IFerryPassenger
{
    /// <summary>
    ///     Provides access to all ferry stations.
    /// </summary>
    IFerryStationLayer FerryStationLayer { get; }
}