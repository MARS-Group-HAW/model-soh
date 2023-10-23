using System;
using Mars.Common.Core;
using Mars.Common.Core.Collections.NonBlockingDictionary;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHCarModel.Parking;

/// <summary>
///     The <code>CarParkingSpace</code> is located somewhere and can hold <code>IParkingCar</code>s up to its capacity
///     extent.
/// </summary>
public class CarParkingSpace : IVectorFeature, IModalChoiceConsumer
{
    private const string Area = "area";

    private ConcurrentDictionary<IParkingCar, byte> _parkingVehicles;

    /// <summary>
    ///     The centroid of this parking space.
    /// </summary>
    public Position Position { get; private set; }

    /// <summary>
    ///     Determines if any free parking spaces are currently available.
    /// </summary>
    public bool HasCapacity => !Occupied && ParkingVehicles.Count < Capacity;

    /// <summary>
    ///     Describes the total capacity ignoring the current occupancy.
    /// </summary>
    public int Capacity { get; private set; }

    /// <summary>
    ///     Defines if the whole car parking space is occupied for any reason (ignoring the currently parked cars).
    ///     These can still leave, but new ones can not enter.
    /// </summary>
    public bool Occupied { get; set; }

    /// <summary>
    ///     Provides all currently parked cars.
    /// </summary>
    public ConcurrentDictionary<IParkingCar, byte> ParkingVehicles =>
        _parkingVehicles ??= new ConcurrentDictionary<IParkingCar, byte>();

    public bool CanConsume()
    {
        return HasCapacity;
    }

    /// <summary>
    ///     Gets or sets the concrete feature data.
    /// </summary>
    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Update(data);
    }

    /// <summary>
    ///     Initializes the <code>ParkingSpace</code> with the feature information.
    /// </summary>
    public void Update(VectorStructuredData data)
    {
        VectorStructured = data;
        var centroid = VectorStructured.Geometry.Centroid;
        Position = Position.CreatePosition(centroid.X, centroid.Y);

        var area = VectorStructured.Data.ContainsKey(Area) ? VectorStructured.Data[Area].Value<double>() : 0;
        if (area < 10) //if smaller than 10m2 then 1 car
            Capacity = 1;
        else if (area < 500) // if smaller than 500m2 then 15m2/car
            Capacity = (int)(area / 15);
        else //if bigger than 100m2 then 20m2/car
            Capacity = (int)(area / 20);
    }

    /// <summary>
    ///     Enter the parking spot with a car and "consume" its required space.
    /// </summary>
    /// <param name="car">The car that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the car is not parked here).</returns>
    public bool Enter(IParkingCar car)
    {
        if (ParkingVehicles.ContainsKey(car))
            throw new ArgumentException("The parking car was already parked");
        if (car.CarParkingSpace != null)
            throw new ArgumentException("The parking car is still parked on another " +
                                        $"parking space '{car.CarParkingSpace.VectorStructured.Geometry}'");

        if (!HasCapacity) return false;

        car.CarParkingSpace = this;
        return ParkingVehicles.TryAdd(car, byte.MinValue);
    }

    /// <summary>
    ///     Leave the parking spot with given car.
    /// </summary>
    /// <param name="car">The car that leaves this spot.</param>
    /// <returns>True if car is not on this parking space any more.</returns>
    public bool Leave(IParkingCar car)
    {
        if (!ParkingVehicles.ContainsKey(car))
            throw new ArgumentException(
                $"The car, leaving the parking spot is not placed at spot '{VectorStructured.Geometry}'");

        var success = ParkingVehicles.TryRemove(car, out _);

        if (success) car.CarParkingSpace = null;

        return success;
    }
}