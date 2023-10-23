using SOHCarModel.Model;
using SOHDomain.Steering.Capables;

namespace SOHCarModel.Steering;

/// <summary>
///     Here are the car specific driver attributes defined for the acceleration calculation.
/// </summary>
public interface ICarSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     The car that is used to drive.
    /// </summary>
    Car Car { get; }

    /// <summary>
    ///     Indicates that the driver is currently driving
    /// </summary>
    bool CurrentlyCarDriving { get; }
}