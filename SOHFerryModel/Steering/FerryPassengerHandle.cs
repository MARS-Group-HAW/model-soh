using Mars.Interfaces.Environments;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;
using SOHFerryModel.Model;

namespace SOHFerryModel.Steering;

/// <summary>
///     This handle provides the position of the ferry and the possibility to leave the ferry.
/// </summary>
public class FerryPassengerHandle : IPassengerHandle
{
    private readonly Ferry _ferry;

    public FerryPassengerHandle(Ferry ferry)
    {
        _ferry = ferry;
    }

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