using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Train.Steering;

/// <summary>
///     This handle provides the position of the train and the possibility to leave the train.
/// </summary>
public class TrainPassengerHandle : IPassengerHandle
{
    private readonly Model.Train _train;

    public TrainPassengerHandle(Model.Train train)
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