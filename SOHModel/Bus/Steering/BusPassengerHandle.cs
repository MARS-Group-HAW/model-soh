using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Bus.Steering;

/// <summary>
///     This handle provides the position of the bus and the possibility to leave the bus.
/// </summary>
public class BusPassengerHandle : VehiclePassengerHandle<IBusSteeringCapable, IPassengerCapable, BusSteeringHandle,
    BusPassengerHandle>
{
    private readonly Model.Bus _bus;

    public BusPassengerHandle(Model.Bus bus) : base(bus)
    {
        _bus = bus;
    }

    /// <summary>
    ///     Leave the bus if it is located in a bus stop.
    /// </summary>
    /// <param name="passengerCapable">Who wants to leave the bus.</param>
    /// <returns>True if the bus could be left. False otherwise.</returns>
    public override bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        if (_bus.BusStation == null) return false;

        _bus.LeaveVehicle(passengerCapable);
        return true;
    }
}