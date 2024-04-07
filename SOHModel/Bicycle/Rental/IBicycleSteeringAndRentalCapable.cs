using SOHModel.Bicycle.Steering;

namespace SOHModel.Bicycle.Rental;

/// <summary>
///     An agent that wishes to rent bicycles for further riding needs to satisfy this interface.
/// </summary>
public interface IBicycleSteeringAndRentalCapable : IBicycleSteeringCapable
{
    /// <summary>
    ///     Provides access to all bicycle rental stations.
    /// </summary>
    IBicycleRentalLayer BicycleRentalLayer { get; }
}