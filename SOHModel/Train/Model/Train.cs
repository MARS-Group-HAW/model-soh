using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Train.Station;
using SOHModel.Train.Steering;

namespace SOHModel.Train.Model;

public class Train : Vehicle<ITrainSteeringCapable, IPassengerCapable, TrainSteeringHandle, TrainPassengerHandle>
{
    public Train()
    {
        IsCollidingEntity = false;
        ModalityType = SpatialModalityType.TrainDriving;
    }

    public TrainLayer Layer { get; set; }

    /// <summary>
    ///     Where the <see cref="Train" /> is located right now. Null if train is not at any station right now.
    /// </summary>
    public TrainStation TrainStation { get; set; }

    protected override TrainPassengerHandle CreatePassengerHandle()
    {
        return new TrainPassengerHandle(this);
    }

    protected override TrainSteeringHandle CreateSteeringHandle(ITrainSteeringCapable driver)
    {
        return new TrainSteeringHandle(Layer.GraphEnvironment, driver, this);
    }
}