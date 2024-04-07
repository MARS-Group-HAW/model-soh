using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.Domain.Steering.Handles;

/// <summary>
///     This steering handle does not actively move but is instead idle waiting like a passengerCapable within a vehicle.
/// </summary>
public class IdlePassengerSteeringHandle : ISteeringHandle
{
    private readonly IPassengerHandle _passengerHandle;

    public IdlePassengerSteeringHandle(IPassengerHandle passengerHandle)
    {
        _passengerHandle = passengerHandle;
    }

    public Position Position => _passengerHandle.Position;

    public bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        return _passengerHandle.LeaveVehicle(passengerCapable);
    }

    public ISpatialGraphEnvironment Environment => null;

    public Route Route { get; set; } //ignore, the driver decides the route

    public bool GoalReached => false;

    public double Velocity => 0d;

    public void Move()
    {
        //do nothing, the vehicle is moving without the influence of the passengerCapable
    }
}