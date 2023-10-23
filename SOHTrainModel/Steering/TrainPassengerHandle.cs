using Mars.Interfaces.Environments;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;
using SOHTrainModel.Model;

namespace SOHTrainModel.Steering;

/// <summary>
///     This handle provides the position of the train and the possibility to leave the train.
/// </summary>
public class TrainPassengerHandle : IPassengerHandle
{
    private readonly Train _train;

    public TrainPassengerHandle(Train train)
    {
        _train = train;
    }

    public Position Position => _train.Position;

    /// <summary>
    ///     Leave the train if it is located in a train station.
    /// </summary>
    /// <param name="passengerCapable">Who wants to leave the train.</param>
    /// <returns>True if the train could be left. False otherwise.</returns>
    public bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        if (_train.TrainStation == null) return false;

        _train.LeaveVehicle(passengerCapable);
        return true;
    }
}