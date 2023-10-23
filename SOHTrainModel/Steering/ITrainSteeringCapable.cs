using SOHDomain.Steering.Capables;
using SOHTrainModel.Route;

namespace SOHTrainModel.Steering;

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