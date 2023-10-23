using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Interfaces.Environments;
using SOHDomain.Common;
using SOHDomain.Model;
using SOHDomain.Steering.Acceleration;
using SOHDomain.Steering.Capables;

namespace SOHDomain.Steering.Handles.Intersection;

public class RightBeforeLeftIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle,
    TPassengerHandle> :
    AbstractIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>
    where TPassengerHandle : IPassengerHandle
    where TSteeringHandle : ISteeringHandle
    where TPassengerCapable : IPassengerCapable
    where TSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Within this distance the vehicle stops to give right of way (if required).
    /// </summary>
    private const double GiveRightOfWayDistanceInM = 10;

    private readonly Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle,
        TPassengerHandle> _vehicle;

    private readonly IVehicleAccelerator _vehicleAccelerator;
    private ISpatialEdge _currentEdge;
    private List<Guid> _orderOfArrival;
    private int _waitedSeconds;

    public RightBeforeLeftIntersectionHandle(
        Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        IVehicleAccelerator vehicleAccelerator)
        : base(vehicle, vehicleAccelerator)
    {
        _vehicle = vehicle;
        _vehicleAccelerator = vehicleAccelerator;
    }

    public override double Evaluate(EdgeExploreResult edgeExplore, DirectionType vehicleDirection)
    {
        double biggestDeceleration = 1000;
        //reset order of arrival for every new edge
        if (_currentEdge == null || _currentEdge != edgeExplore.Edge)
        {
            _currentEdge = edgeExplore.Edge;
            _orderOfArrival = new List<Guid>();
        }

        //reduce speed for intersection
        if (_vehicle.Velocity >= VehicleConstants.IntersectionSpeed)
        {
            var speedChange = _vehicleAccelerator.CalculateSpeedChange(_vehicle.Velocity,
                edgeExplore.Edge.MaxSpeed, edgeExplore.IntersectionDistance,
                VehicleConstants.IntersectionSpeed);

            if (_vehicle.Velocity + speedChange < VehicleConstants.IntersectionSpeed)
            {
                var a = VehicleConstants.IntersectionSpeed - _vehicle.Velocity;
                if (a < biggestDeceleration)
                    biggestDeceleration = a;
            }
            else
            {
                if (speedChange < biggestDeceleration)
                    biggestDeceleration = speedChange;
            }
        }
        else
        {
            var speedChange = _vehicleAccelerator.CalculateSpeedChange(_vehicle.Velocity,
                VehicleConstants.IntersectionSpeed, 1000, VehicleConstants.IntersectionSpeed);

            if (speedChange + _vehicle.Velocity > VehicleConstants.IntersectionSpeed)
                speedChange = VehicleConstants.IntersectionSpeed - _vehicle.Velocity;

            if (speedChange < biggestDeceleration)
                biggestDeceleration = speedChange;
        }

        if (_vehicle.Velocity < 0.01) _waitedSeconds++;

        //resolve eventual deadlock
        if (_waitedSeconds > 10)
        {
            var incomingCars = CollectIncomingEntities(edgeExplore.Edge.To);
            if (_orderOfArrival.Count == 0)
            {
                var arrivalOrder = incomingCars.OrderBy(kvp => kvp.CurrentEdge.Length - kvp.PositionOnCurrentEdge);
                _orderOfArrival.AddRange(arrivalOrder.Select(keyValuePair => keyValuePair.ID));
            }
            else
            {
                var isSubset = _orderOfArrival.All(guid => incomingCars.Select(entity => entity.ID).Contains(guid));

                if (isSubset && incomingCars.First() == _vehicle)
                {
                    var speedChange =
                        _vehicleAccelerator.CalculateSpeedChange(_vehicle.Velocity,
                            edgeExplore.Edge.MaxSpeed, 1000, 14);
                    _vehicle.Velocity = 1.5;
                    if (speedChange < biggestDeceleration) biggestDeceleration = speedChange;

                    return biggestDeceleration;
                }
            }
        }

        //regular behavior
        foreach (var incomingEdge in edgeExplore.Edge.To.IncomingEdges.Values)
        {
            if (incomingEdge == _vehicle.CurrentEdge) continue;

            var currentEdgeBearing =
                _vehicle.CurrentEdge.From.Position.GetBearing(_vehicle.CurrentEdge.To.Position);
            var incomingBearing =
                incomingEdge.From.Position.GetBearing(incomingEdge.To.Position); //TODO use geometry
            var otherEdgeDirection = PositionHelper.GetDirectionType(incomingBearing, currentEdgeBearing);

            if (GiveRightOfWayConstellation(otherEdgeDirection, vehicleDirection))
                foreach (var roadUser in ExploreIncomingEdge(incomingEdge))
                {
                    var remainingDistanceOnEdge = Math.Max(0,
                        roadUser.CurrentEdge.Length - roadUser.PositionOnCurrentEdge);
                    if (remainingDistanceOnEdge < GiveRightOfWayDistanceInM && remainingDistanceOnEdge > 0)
                    {
                        var speedChange = _vehicleAccelerator.CalculateSpeedChange(_vehicle.Velocity,
                            edgeExplore.Edge.MaxSpeed, edgeExplore.IntersectionDistance, 0);
                        if (speedChange < biggestDeceleration)
                            biggestDeceleration = speedChange;
                    }
                }
        }

        if (biggestDeceleration > 0) _waitedSeconds = 0;

        return biggestDeceleration;
    }

    private static bool GiveRightOfWayConstellation(DirectionType otherEdgeDirection,
        DirectionType vehicleDirection)
    {
        if (otherEdgeDirection == DirectionType.Up)
            return !(vehicleDirection == DirectionType.UpRight || vehicleDirection == DirectionType.Right ||
                     vehicleDirection == DirectionType.DownRight || vehicleDirection == DirectionType.Up);
        return otherEdgeDirection == DirectionType.UpRight || otherEdgeDirection == DirectionType.Right ||
               otherEdgeDirection == DirectionType.DownRight;
    }
}