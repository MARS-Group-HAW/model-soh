using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Ferry.Steering;

/// <summary>
///     This handle provides the position of the ferry and the possibility to leave the ferry.
/// </summary>
public class FerryPassengerHandle : IPassengerHandle
{
    private readonly Model.Ferry _ferry;

    /// <summary>
    ///     Creates a new passenger handler for an existing ferry to leave this vehicle.
    /// </summary>
    /// <param name="ferry"></param>
    public FerryPassengerHandle(Model.Ferry ferry)
    {
        _ferry = ferry;
    }

    /// <summary>
    ///     Gives the current position of the vehicle and thus all its passengers.
    /// </summary>
    public Position Position => _ferry.Position;

    /// <summary>
    ///     Leave the ferry if it is located in a ferry station.
    /// </summary>
    /// <param name="passengerCapable">Who wants to leave the ferry.</param>
    /// <returns>True if the ferry could be left. False otherwise.</returns>
    public bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        if (_ferry.FerryStation == null) return false;

        _ferry.LeaveVehicle(passengerCapable);
        return true;
    }
}