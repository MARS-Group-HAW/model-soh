using Mars.Interfaces.Environments;
using SOHDomain.Steering.Capables;

namespace SOHDomain.Steering.Handles;

/// <summary>
///     The <class>IPassengerVehicle</class> provides the possibility to leave the vehicle.
/// </summary>
public interface IPassengerHandle
{
    /// <summary>
    ///     Gives the current position of the vehicle and thus all its passengers.
    /// </summary>
    Position Position { get; }

    /// <summary>
    ///     Provides the possibility for a passengerCapable to leave the vehicle.
    /// </summary>
    /// <param name="passengerCapable">That will leave the vehicle.</param>
    /// <returns></returns>
    bool LeaveVehicle(IPassengerCapable passengerCapable);
}