using SOHModel.Domain.Steering.Capables;
using SOHModel.Train.Route;

namespace SOHModel.Train.Steering;

/// <summary>
///     A capable subclass is able to drive a train.
/// </summary>
public interface ITrainSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Describes the route along that the driver moves his/her train
    /// </summary>
    public TrainRoute TrainRoute { get; }
}