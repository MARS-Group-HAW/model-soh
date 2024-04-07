using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Common;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Domain.Model;

/// <summary>
///     A vehicle entrance allows passenger to use vehicles in different roles (as driver or co-driver/passenger).
/// </summary>
public interface IVehicleEntrance<in TDriver, in TPassenger, TSteeringHandle, TPassengerHandle>
    where TDriver : ISteeringCapable
    where TPassenger : IPassengerCapable
    where TSteeringHandle : ISteeringHandle
    where TPassengerHandle : IPassengerHandle
{
    /// <summary>
    ///     The passenger tries to enter the vehicle in the driver role. On success the driver is able to steer the
    ///     vehicle with the given handle.
    ///     <bold>ATTENTION:</bold>This method only affects the vehicle parameters. The broader context (driver was
    ///     probably a pedestrian in another environment) has to be taken into consideration and adjusted to the new
    ///     modal context.
    /// </summary>
    /// <param name="driver">That will drive the vehicle.</param>
    /// <param name="handle">The steering handle that can be used by the driver to control the vehicle.</param>
    /// <returns>Whether the driver could enter the vehicle or not.</returns>
    bool TryEnterDriver(TDriver driver, out TSteeringHandle handle);

    /// <summary>
    ///     The passenger tries to enter the vehicle as co-driver.
    ///     <bold>ATTENTION:</bold>This method only affects the vehicle parameters. The broader context (passenger was
    ///     probably a pedestrian in another environment) has to be taken into consideration and adjusted to the new
    ///     modal context.
    /// </summary>
    /// <param name="passenger">That will board the vehicle for transportation.</param>
    /// <param name="handle">The passenger handle that provides the possibility to be moved.</param>
    /// <returns>Whether the passenger could enter the vehicle or not.</returns>
    bool TryEnterPassenger(TPassenger passenger, out TPassengerHandle handle);

    /// <summary>
    ///     Notifies all passengers and the driver.
    /// </summary>
    /// <param name="passengerMessage">Notification message for all passengers.</param>
    void NotifyPassengers(PassengerMessage passengerMessage);
}