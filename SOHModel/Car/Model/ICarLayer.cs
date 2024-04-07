using Mars.Interfaces.Environments;
using SOHModel.Car.Parking;
using SOHModel.Domain.Model;

namespace SOHModel.Car.Model;

/// <summary>
///     Provides access to car relevant resources.
/// </summary>
public interface ICarLayer : IModalLayer
{
    /// <summary>
    ///     Holds the environment that can be used for cars to move.
    /// </summary>
    ISpatialGraphEnvironment Environment { get; }

    /// <summary>
    ///     Holds all parking opportunities for cars.
    /// </summary>
    ICarParkingLayer CarParkingLayer { get; }
}