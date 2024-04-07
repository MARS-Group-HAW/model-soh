using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Model;

namespace SOHModel.Car.Parking;

/// <summary>
///     The <code>ICarParkingLayer</code> capsules the access to all <code>CarParkingSpace</code>s.
/// </summary>
public interface ICarParkingLayer : IVectorLayer<CarParkingSpace>, IModalLayer
{
    /// <summary>
    ///     Tries to find the nearest <code>CarParkingSpace</code> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="freeCapacity">Only finds parking spots that are free.</param>
    /// <returns>The corresponding <code>CarParkingSpace</code> if one is found, null otherwise.</returns>
    CarParkingSpace? Nearest(Position position, bool freeCapacity = true);

    /// <summary>
    ///     Occupies a percentage of parking spaces.
    /// </summary>
    /// <param name="percent">Defines how many parking spaces are occupied, value between 0.0 and 1.0.</param>
    /// <param name="carCount">
    ///     How many cars are in the scenario, this amount will be integrated, because these cars
    ///     also occupy spaces.
    /// </param>
    void UpdateOccupancy(double percent, int carCount = 0);

    /// <summary>
    ///     Creates a car on a parking space within given radius. Takes closest parking space if radius is smaller/eqal zero.
    /// </summary>
    /// <param name="position">Where the car is about to be placed</param>
    /// <param name="radiusInM">Limits the distance to the position.</param>
    /// <param name="keyAttribute">Identifies the attribute that describes the car type.</param>
    /// <param name="type">Determines the car type.</param>
    /// <returns>An initialized car on a parking space.</returns>
    Model.Car CreateOwnCarNear(Position position, double radiusInM = -1, string keyAttribute = "type", string type = "Golf");
}