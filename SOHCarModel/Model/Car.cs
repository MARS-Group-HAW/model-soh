using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHCarModel.Parking;
using SOHCarModel.Steering;
using SOHDomain.Graph;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;

namespace SOHCarModel.Model;

/// <summary>
///     Implementation of a regular car that has dimensions and some driving params and can be used for driving and
///     co-driving.
/// </summary>
public class Car : Vehicle<ICarSteeringCapable, IPassengerCapable, CarSteeringHandle, CarPassengerHandle>,
    IParkingCar
{
    private ISpatialGraphEnvironment _environment;

    public Car()
    {
        ModalityType = SpatialModalityType.CarDriving;
    }

    [PropertyDescription] public StreetLayer StreetLayer { get; set; }

    /// <summary>
    ///     Holds the graph on which the car is moving.
    /// </summary>
    public ISpatialGraphEnvironment Environment
    {
        get => _environment ?? StreetLayer.Environment;
        set => _environment = value;
    }

    /// <summary>
    ///     Gets or sets the distance able to enter this car in <c>meter (m)</c>
    /// </summary>
    [PropertyDescription(Name = "distanceToEnterCar", Ignore = true)]
    public double DistanceToEnterCar { get; } = 100;

    public CarParkingLayer CarParkingLayer { get; set; }

    public CarParkingSpace CarParkingSpace { get; set; }

    protected override CarPassengerHandle CreatePassengerHandle()
    {
        return new CarPassengerHandle(this);
    }

    protected override CarSteeringHandle CreateSteeringHandle(ICarSteeringCapable steeringCapable)
    {
        return new CarSteeringHandle(Environment, this);
    }

    protected override bool IsInRangeToEnterVehicle(IPassengerCapable passenger)
    {
        if (Position == null || passenger.Position == null) return false;

        var carNode = Environment.NearestNode(Position);
        var passengerNode = Environment.NearestNode(passenger.Position);

        var distanceInMTo = Position.DistanceInMTo(passenger.Position);
        return carNode.Equals(passengerNode) || distanceInMTo < DistanceToEnterCar;

        //TODO use if entering over edge instead of entering over node
        // return Position.DistanceInMTo(passenger.Position) < DistanceToEnterCarInM; 
    }
}