using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHDomain.Steering.Capables;

namespace SOHBicycleModel.Steering;

/// <summary>
///     Here are the car specific driver attributes defined for the acceleration calculation.
/// </summary>
public interface IBicycleSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Determines a driver dependent behaviour value.
    ///     Value should be between 0.0 and 1.0 and influences how careful / aggressive the driver is
    /// </summary>
    double DriverRandom { get; }

    /// <summary>
    ///     Defines the type of driver.
    /// </summary>
    DriverType DriverType { get; }

    /// <summary>
    ///     Determines how much power the driver has in watt. 75 is the mean value with a standard deviation of 3.
    /// </summary>
    double CyclingPower { get; }

    /// <summary>
    ///     Determines the weight of the cyclist
    /// </summary>
    double Mass { get; }

    /// <summary>
    ///     Determines the gradient of the speed change calculation.
    /// </summary>
    public double Gradient { get; }

    /// <summary>
    ///     The bicycle that the capable might own.
    /// </summary>
    Bicycle Bicycle { get; }
}