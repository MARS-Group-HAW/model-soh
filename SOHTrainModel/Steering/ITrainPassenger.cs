using SOHTrainModel.Station;

namespace SOHTrainModel.Steering;

/// <summary>
///     Provides access to the train stations, which is necessary to use the train.
/// </summary>
public interface ITrainPassenger
{
    /// <summary>
    ///     Provides access to all train stations.
    /// </summary>
    ITrainStationLayer TrainStationLayer { get; }
}