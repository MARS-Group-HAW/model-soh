using System.Collections.Generic;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Common;

namespace SOHDomain.Model;

public abstract partial class Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>
{
    private List<IPassengerCapable> _passengers;

    /// <summary>
    ///     Gets or sets the collection of passenger entered in this vehicle.
    /// </summary>
    public List<IPassengerCapable> Passengers
    {
        get => _passengers ??= new List<IPassengerCapable>();
        set => _passengers = value;
    }

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
    public virtual bool TryEnterDriver(TSteeringCapable driver, out TSteeringHandle handle)
    {
        if (Driver != null || Passengers.Contains(driver) || !IsInRangeToEnterVehicle(driver))
        {
            handle = default;
            return false;
        }

        Driver = driver;
        handle = CreateSteeringHandle(driver);
        return true;
    }

    /// <summary>
    ///     The passenger tries to enter the vehicle as co-driver.
    ///     <bold>ATTENTION:</bold>This method only affects the vehicle parameters. The broader context (passenger was
    ///     probably a pedestrian in another environment) has to be taken into consideration and adjusted to the new
    ///     modal context.
    /// </summary>
    /// <param name="passenger">That will board the vehicle for transportation.</param>
    /// <param name="handle">The passenger handle that provides the possibility to be moved.</param>
    /// <returns>Whether the passenger could enter the vehicle or not.</returns>
    public virtual bool TryEnterPassenger(TPassengerCapable passenger, out TPassengerHandle handle)
    {
        if (!HasFreeCapacity() || Passengers.Contains(passenger) || passenger.Equals(Driver) ||
            !IsInRangeToEnterVehicle(passenger))
        {
            handle = default;
            return false;
        }

        Passengers.Add(passenger);
        handle = CreatePassengerHandle();
        return true;
    }

    /// <summary>
    ///     Notifies all passengers and the driver.
    /// </summary>
    /// <param name="passengerMessage">Notification message for all passengers.</param>
    public virtual void NotifyPassengers(PassengerMessage passengerMessage)
    {
        //TODO test if driver and passengers are notified when leaving the vehicle
        Driver?.Notify(passengerMessage);

        foreach (var passenger in Passengers.ToArray()) passenger.Notify(passengerMessage);
    }

    public virtual void LeaveVehicle(TPassengerCapable passenger)
    {
        if (passenger is ISteeringCapable steeringCapable && Driver == steeringCapable)
            Driver = null;

        if (Passengers.Contains(passenger))
            Passengers.Remove(passenger);
    }

    /// <summary>
    ///     Hook-method to check if entering a vehicle should be allowed,
    ///     based on the distance between passenger and vehicle.
    /// </summary>
    /// <param name="passenger">Of which the distance to this vehicle is tested.</param>
    /// <returns>True, if the range check succeeds, false otherwise.</returns>
    protected virtual bool IsInRangeToEnterVehicle(IPassengerCapable passenger)
    {
        return true;
    }
}