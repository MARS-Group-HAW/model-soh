using Mars.Interfaces.Environments;
using SOHModel.Car.Parking;

namespace SOHModel.Car.Model;

/// <summary>
///     Simple car with reasonable parameters.
/// </summary>
public class Golf : Car
{
    private Golf()
    {
        Length = 4.5;
        MaxAcceleration = 0.73;
        MaxDeceleration = 1.67;
        MaxSpeed = 38.89;
        Velocity = 0;
        PassengerCapacity = 4;
        ModalityType = SpatialModalityType.CarDriving;
    }

    /// <summary>
    ///     Creates a car at the given position.
    /// </summary>
    /// <param name="carParkingLayer">Holds all relevant car resources.</param>
    /// <param name="position">Start position of the car.</param>
    /// <returns>The generated car</returns>
    public static Golf Create(CarParkingLayer carParkingLayer, Position position = null)
    {
        return new Golf { Position = position, CarParkingLayer = carParkingLayer };
    }

    /// <summary>
    ///     Creates a car at the given position.
    /// </summary>
    /// <param name="environment">Holds the movement graph.</param>
    /// <param name="position">Start position of the car.</param>
    /// <returns>The generated car</returns>
    public static Golf Create(ISpatialGraphEnvironment environment, Position position = null)
    {
        return new Golf { Position = position, Environment = environment };
    }

    /// <summary>
    ///     Creates a car on the nearest ParkingSpot that is found in the parking layer next to the parking spot position.
    /// </summary>
    /// <param name="carParkingLayer">Holds all relevant car resources.</param>
    /// <param name="environment">The graph on which the car will move.</param>
    /// <param name="parkingSpotPosition">A position where a parking spot may be.</param>
    /// <returns>The generated car that is inserted on the found parking spot</returns>
    /// <exception cref="ArgumentException">If parkingLayer or parkingSpotPosition are null.</exception>
    public static Golf CreateOnParking(CarParkingLayer carParkingLayer, ISpatialGraphEnvironment environment,
        Position parkingSpotPosition)
    {
        if (carParkingLayer == null || parkingSpotPosition == null || environment == null)
        {
            var parkingNull = carParkingLayer != null ? "not null" : "null";
            var envNull = environment != null ? "not null" : "null";
            var posNull = parkingSpotPosition != null ? "not null" : "null";

            throw new ArgumentException(
                $"All parameters must be not null: carParkingLayer({parkingNull}), environment({envNull}), parkingSpotPosition({posNull})");
        }

        var car = new Golf
        {
            CarParkingLayer = carParkingLayer,
            Environment = environment
        };

        var parkingSpace = carParkingLayer.Nearest(parkingSpotPosition);
        if (parkingSpace == null) throw new ApplicationException("No free parking spot found. No Car created.");

        var counter = 0;
        while (!parkingSpace.Enter(car))
        {
            parkingSpace = carParkingLayer.Nearest(parkingSpotPosition);
            counter++;
            if (counter > 10)
                throw new ApplicationException(
                    "Could not add a car to the parking layer. Probably the gis file is corrupted.");
        }

        car.Position = car.Environment.NearestNode(parkingSpace.Position).Position;
        return car;
    }
}