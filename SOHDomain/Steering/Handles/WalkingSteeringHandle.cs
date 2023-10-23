using System;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHDomain.Graph;
using SOHDomain.Model;
using SOHDomain.Steering.Acceleration;
using SOHDomain.Steering.Capables;

namespace SOHDomain.Steering.Handles;

/// <summary>
///     The <code>WalkingSteeringHandle</code> provides the possibility to walk along a <see cref="Route" />
/// </summary>
public class WalkingSteeringHandle : ISteeringHandle
{
    private readonly WalkingAccelerator _accelerator;

    private readonly int _deltaTinSeconds;
    private readonly IWalkingCapable _walkingCapable;
    private bool _positionValidationRequired;
    private Route _route;

    public WalkingSteeringHandle(IWalkingCapable walkingCapable, ISpatialGraphLayer graphLayer)
    {
        Environment = graphLayer.Environment;

        _walkingCapable = walkingCapable;
        _deltaTinSeconds = graphLayer.Context.OneTickTimeSpan?.Seconds ?? 1;
        _accelerator = new WalkingAccelerator(walkingCapable);
        Position = walkingCapable.Position;
    }

    private WalkingShoes WalkingShoes => _walkingCapable.WalkingShoes;

    public ISpatialGraphEnvironment Environment { get; }

    public Route Route
    {
        get => _route;
        set => _route = UpdateDesiredLanes(value);
    }

    public bool GoalReached => Route == null || Route.GoalReached;

    public double Velocity => WalkingShoes.Velocity;

    public void Move()
    {
        if (GoalReached) return;

        var distance = CalculateMovementDistance();

        if (!Environment.Entities.ContainsKey(WalkingShoes)) // should not occur, but for stability reasons in here
            Environment.Insert(WalkingShoes, Route.First().Edge.From);

        if (!Environment.Move(WalkingShoes, Route, distance))
            throw new ArgumentException(
                $"{nameof(WalkingSteeringHandle)} should always be able to move");

        _positionValidationRequired = true;
        if (GoalReached)
            WalkingShoes.Velocity = 0;
        else
            WalkingShoes.Velocity = Math.Round(distance, 2, MidpointRounding.AwayFromZero) * _deltaTinSeconds;
    }

    public Position Position
    {
        get
        {
            if (_positionValidationRequired)
            {
                _positionValidationRequired = false;
                WalkingShoes.Position = WalkingShoes.CalculateNewPositionFor(Route, out var bearing);
                WalkingShoes.Bearing = bearing;
            }

            return WalkingShoes.Position;
        }
        set
        {
            _positionValidationRequired = false;
            WalkingShoes.Position = value;
        }
    }

    public bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        WalkingShoes.LeaveVehicle(passengerCapable);
        return true;
    }

    private static Route UpdateDesiredLanes(Route route)
    {
        foreach (var edgeStop in route)
            edgeStop.DesiredLane = edgeStop.Edge.ModalityLaneRanges[SpatialModalityType.Walking].Item1;

        return route;
    }

    private double CalculateMovementDistance()
    {
        var distanceToMove = _accelerator.CalculateVelocity(Route.Stops.First().Edge) * _deltaTinSeconds;
        var distanceToGoal = Route.RemainingRouteDistanceToGoal;
        return Math.Min(distanceToMove, distanceToGoal);
    }
}