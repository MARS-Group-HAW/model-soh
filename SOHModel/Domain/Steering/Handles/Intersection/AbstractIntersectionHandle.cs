using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Acceleration;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.Domain.Steering.Handles.Intersection;

/// <summary>
///     Provides the possibility to handle intersections without traffic lights.
/// </summary>
/// <typeparam name="TSteeringCapable"></typeparam>
/// <typeparam name="TPassengerCapable"></typeparam>
/// <typeparam name="TSteeringHandle"></typeparam>
/// <typeparam name="TPassengerHandle"></typeparam>
public abstract class AbstractIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle,
    TPassengerHandle> : IIntersectionTrafficCode
    where TPassengerHandle : IPassengerHandle
    where TSteeringHandle : ISteeringHandle
    where TPassengerCapable : IPassengerCapable
    where TSteeringCapable : ISteeringCapable
{
    protected AbstractIntersectionHandle(
        Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        IVehicleAccelerator vehicleAccelerator)
    {
        VehicleAccelerator = vehicleAccelerator;
        Vehicle = vehicle;
    }

    /// <summary>
    ///     Acceleration module of the vehicle.
    /// </summary>
    protected IVehicleAccelerator VehicleAccelerator { get; }

    /// <summary>
    ///     Vehicle that is using this handle.
    /// </summary>
    protected Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> Vehicle { get; }

    /// <summary>
    ///     Provides a speed change for given situation that is described in the parameters.
    /// </summary>
    /// <param name="edgeExploreResult">Holds information about the relevant environment status.</param>
    /// <param name="vehicleDirection">In which the driver wants to drive.</param>
    /// <returns>The acceleration / deceleration ratio.</returns>
    public abstract double Evaluate(EdgeExploreResult edgeExploreResult, DirectionType vehicleDirection);

    /// <summary>
    ///     Performs an exploration query on the specified <c>incomingEdge</c> and
    ///     collects each first appearing entity which is coming from this edge.
    /// </summary>
    /// <param name="incomingEdge">The edge from which to explore.</param>
    /// <param name="distance">The distance in meter to explore</param>
    /// <returns>
    ///     Returns an <see cref="IEnumerable{T}" /> of <see cref="ISpatialGraphEntity" />s which
    ///     are within the desired explore distance.
    /// </returns>
    protected static IEnumerable<ISpatialGraphEntity> ExploreIncomingEdge(ISpatialEdge incomingEdge,
        double distance = 100)
    {
        return incomingEdge.Explore(incomingEdge.Length,
                distance, true, ExploreDirection.Backward).LaneExplores.Values
            .Where(result => result.Backward.Count > 0).Select(result => result.Backward.First());
    }

    protected static IEnumerable<ISpatialGraphEntity> CollectIncomingEntities(ISpatialNode spatialNode)
    {
        // Collect each FIRST incoming entity from the intersection. Each entity does know on which lane it is 
        return spatialNode.IncomingEdges.Values.SelectMany(edge => ExploreIncomingEdge(edge));
    }
}