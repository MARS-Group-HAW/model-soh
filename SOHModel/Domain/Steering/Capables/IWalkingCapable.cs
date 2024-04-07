using SOHModel.Domain.Model;

namespace SOHModel.Domain.Steering.Capables;

/// <summary>
///     Defines the walking entities specific characteristics
/// </summary>
public interface IWalkingCapable : ISteeringCapable
{
    /// <summary>
    ///     Provides the preferred speed of a walking entity.
    /// </summary>
    double PreferredSpeed { get; }

    /// <summary>
    ///     The physical representation on the road.
    /// </summary>
    WalkingShoes WalkingShoes { get; }

    double PerceptionInMeter { get; set; }
}