using Mars.Interfaces.Environments;

namespace SOHModel.Domain.Steering.Handles.Intersection;

/// <summary>
///     Contract to define the deceleration of an entity when crossing an intersection.
/// </summary>
public interface IIntersectionTrafficCode
{
    /// <summary>
    ///     Evaluates the deceleration that is required to handle the intersection.
    /// </summary>
    /// <param name="edgeExploreResult">contains information about the intersection.</param>
    /// <param name="vehicleDirection">indicates the turning angle.</param>
    /// <returns>The required biggest deceleration.</returns>
    double Evaluate(EdgeExploreResult edgeExploreResult, DirectionType vehicleDirection);
}