using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.Domain.Steering.Acceleration;

/// <summary>
///     Provides the acceleration calculation for a pedestrian.
/// </summary>
public class WalkingAccelerator
{
    private readonly IWalkingCapable _walkingCapable;

    public WalkingAccelerator(IWalkingCapable walkingCapable)
    {
        _walkingCapable = walkingCapable;
    }

    /// <summary>
    ///     Calculate the possible velocity of an agent depending on his or her current speed, their mean speed and the density
    ///     on the edge.
    /// </summary>
    /// <param name="edge">That provides information about the density.</param>
    /// <returns>The velocity that is reasonable depending on the given parameters.</returns>
    public double CalculateVelocity(ISpatialEdge edge)
    {
        if (_walkingCapable.PreferredSpeed <= 0)
            throw new ApplicationException("Gender is not set for agent and therefore no preferred speed.");

        var density = CalculateDensity(edge, _walkingCapable.PerceptionInMeter);
        var velocity = PossibleSpeedForDensity(_walkingCapable.PreferredSpeed, density);
        return velocity > 0 ? velocity : _walkingCapable.WalkingShoes.Velocity;
    }

    /// <summary>
    ///     Explore the agents in front of the agent and calculate the density.
    /// </summary>
    /// <param name="edge">That provides information about the density.</param>
    /// <param name="perceptionInMeter">Describes how distant other pedestrians may be to affect density.</param>
    /// <returns>Density. Agents per square meter.</returns>
    private double CalculateDensity(ISpatialEdge edge, double perceptionInMeter)
    {
        var density = 0.0;
        if (edge == null || !edge.Entities.Contains(_walkingCapable.WalkingShoes)) return density;

        var counts = edge.ExploreInLaneOnEdge(_walkingCapable.WalkingShoes, perceptionInMeter);
        if (counts <= 0) return density;

        density = counts / (2.1 * perceptionInMeter);
        return density;
    }

    /// <summary>
    ///     Calculates human velocity for given density according to LOS-Concept by Weidmann.
    /// </summary>
    /// <param name="velocity">actual speed of agent</param>
    /// <param name="density">actual density per square meter in front of the agent</param>
    /// <returns>possible speed of the agent</returns>
    private static double PossibleSpeedForDensity(double velocity, double density)
    {
        var speedForDensity = velocity;
        if (density < 0.38)
        {
            //no significant obstructions: do nothing
        }

        if (density > 0.38 && density < 0.53)
            // Speed is max 99% from fastest
            speedForDensity *= 0.99;

        if (density > 0.53 && density < 0.68)
            // Speed is max 96% from fastest
            speedForDensity *= 0.96;

        if (density > 0.68 && density < 0.88)
            // Speed is max 91% from fastest
            speedForDensity *= 0.91;

        if (density > 0.88 && density < 1.25)
            // Speed is max 84% from fastest
            speedForDensity *= 0.84;

        if (density > 1.25 && density < 1.75)
            // Speed is max 69% from fastest
            speedForDensity *= 0.69;

        if (density > 1.75 && density < 3.95)
            // Speed is max 52% from fastest
            speedForDensity *= 0.52;

        if (density > 3.95)
            // Movement speed at 12% thus the agent can escape from this situation
            speedForDensity *= 0.12;

        return speedForDensity;
    }
}