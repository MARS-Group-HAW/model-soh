using Mars.Interfaces.Environments;
using SOHModel.SemiTruck.Steering;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.SemiTruck.Model;

public class SemiTruck : Vehicle<ISemiTruckSteeringCapable, IPassengerCapable, SemiTruckSteeringHandle, SemiTruckPassengerHandle>
{
    private ISpatialGraphEnvironment _environment;

    public SemiTruck()
    {
        IsCollidingEntity = true;
        ModalityType = SpatialModalityType.CarDriving; // Similar driving modality as cars
    }

    /// <summary>
    ///     Reference to the truck layer that manages all truck entities in the simulation.
    /// </summary>
    public SemiTruckLayer Layer { get; set; }

    /// <summary>
    ///     Environment in which the truck operates, typically a road network.
    /// </summary>
    public ISpatialGraphEnvironment Environment
    {
        get => _environment ?? Layer.GraphEnvironment;
        set => _environment = value;
    }

    /// <summary>
    ///     Defines a maximum distance within which passengers can enter the truck (if relevant).
    ///     Adjust this value as needed for the truck's characteristics.
    /// </summary>
    public double DistanceToEnterTruck { get; } = 150;

    /// <summary>
    ///     Creates a handle for managing passenger interactions with the truck.
    /// </summary>
    protected override SemiTruckPassengerHandle CreatePassengerHandle()
    {
        return new SemiTruckPassengerHandle(this);
    }

    /// <summary>
    ///     Creates a handle for managing the truck's steering behavior.
    /// </summary>
    protected override SemiTruckSteeringHandle CreateSteeringHandle(ISemiTruckSteeringCapable steeringCapable)
    {
        return new SemiTruckSteeringHandle(Environment, this);
    }

    /// <summary>
    ///     Checks if a passenger is in range to enter the truck.
    /// </summary>
    protected override bool IsInRangeToEnterVehicle(IPassengerCapable passenger)
    {
        if (Position == null || passenger.Position == null) return false;

        var truckNode = Environment.NearestNode(Position);
        var passengerNode = Environment.NearestNode(passenger.Position);

        var distanceInMTo = Position.DistanceInMTo(passenger.Position);
        return truckNode.Equals(passengerNode) || distanceInMTo < DistanceToEnterTruck;
    }
}
