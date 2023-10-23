using Mars.Interfaces.Environments;
using SOHCarModel.Parking;
using SOHDomain.Model;

namespace SOHCarModel.Model;

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