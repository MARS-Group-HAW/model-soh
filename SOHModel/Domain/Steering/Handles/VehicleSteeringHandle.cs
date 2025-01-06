using Mars.Common;
using Mars.Components.Environments;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Car.Model;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Acceleration;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles.Intersection;

namespace SOHModel.Domain.Steering.Handles;

public class VehicleSteeringHandle
    <TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> :
        VehiclePassengerHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>,
        ISteeringHandle
    where TSteeringCapable : ISteeringCapable
    where TPassengerCapable : IPassengerCapable
    where TSteeringHandle : ISteeringHandle
    where TPassengerHandle : IPassengerHandle
{
    protected const double MaximalDeceleration = 1000;
    private const int IntersectionAheadClearanceInM = 150;
    private const int FreeDrivingClearanceInM = 1000;
    protected const double UrbanSafetyDistanceInM = 50d;
    private const double FiftyKmHinMs = 50d / 3.6;
    private const int MinimalExploreDistance = 30;
    private const double SafetyDistanceForOvertaking = 20; //TODO literature?

    private IIntersectionTrafficCode _intersectionTrafficCode;
    protected IVehicleAccelerator VehicleAccelerator;

    public VehicleSteeringHandle(ISpatialGraphEnvironment environment,
        Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        double standardSpeedLimit = FiftyKmHinMs) : base(vehicle)
    {
        Environment = environment;
        Route = new Route();
        StandardSpeedLimit = standardSpeedLimit;
        VehicleAccelerator = new IntelligentDriverAccelerator();
        SetIntersectionTrafficCode(vehicle.TrafficCode);
        NextTrafficLightPhase = TrafficLightPhase.None;
    }

    public double RemainingDistanceOnEdge => Vehicle.RemainingDistanceOnEdge;

    public double SpeedLimit => Vehicle.CurrentEdge?.MaxSpeed ?? StandardSpeedLimit;
    private double StandardSpeedLimit { get; }

    private double MaxSpeed => Math.Min(SpeedLimit, Vehicle.MaxSpeed);

    public TrafficLightPhase NextTrafficLightPhase { get; protected set; }

    public Route Route { get; set; }

    public ISpatialGraphEnvironment Environment { get; }
    public double Velocity => Vehicle.Velocity;
    public bool GoalReached => Route?.GoalReached ?? true;

    public virtual void Move()
    {
        if (GoalReached || (Vehicle.CurrentEdge == null && !MoveFromNodeSuccessfully())) return;

        //TODO stay on lane when going on next edge?? going right left, before turning on intersection?

        var exploreResult = ExploreEnvironment();
        var deceleration = MaximalDeceleration;
        deceleration = HandleBraking(deceleration);
        deceleration = HandleIntersectionAhead(exploreResult, deceleration);
        deceleration = HandleVehiclesAhead(exploreResult, deceleration);

        var drivingDistance = CalculateDrivingDistance(deceleration);
        PerformMoveAction(drivingDistance);
    }

    private double HandleBraking(double deceleration)
    {
        if (!Vehicle.Driver.BrakingActivated)
            return deceleration;

        var distance = Math.Pow(Velocity * 3.6 / 10, 2) * 2;
        return CalculateSpeedChange(Velocity, MaxSpeed, distance, 0, 0);
    }

    public void SetIntersectionTrafficCode(string trafficCode)
    {
        _intersectionTrafficCode = trafficCode switch
        {
            "south-african" => new FifoIntersectionHandle
                <TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>(Vehicle,
                    VehicleAccelerator),
            _ => new RightBeforeLeftIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle,
                TPassengerHandle>(Vehicle, VehicleAccelerator)
        };
    }

    protected SpatialGraphExploreResult ExploreEnvironment()
    {
        
        return Environment.Explore(Vehicle, Route,
            Math.Max(MinimalExploreDistance, Vehicle.ExploreDistanceFactor * Vehicle.Velocity));
    }

    protected virtual double HandleIntersectionAhead(SpatialGraphExploreResult exploreResult,
        double biggestDeceleration)
    {
        NextTrafficLightPhase = exploreResult.EdgeExplores.FirstOrDefault()?.LightPhase ?? TrafficLightPhase.None;

        if (Vehicle.RemainingDistanceOnEdge > IntersectionAheadClearanceInM)
            return biggestDeceleration;

        var exploreResults = exploreResult.EdgeExplores;
        var approachingEndOfRoute =
            Route.RemainingRouteDistanceToGoal < UrbanSafetyDistanceInM && exploreResults.Count == 1;
        if (approachingEndOfRoute)
        {
            var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed,
                exploreResults.First().IntersectionDistance, 0, 0);
            return Math.Min(speedChange, biggestDeceleration);
        }

        for (var index = 0; index < exploreResults.Count - 1; index++)
        {
            var edgeExploreResult = exploreResults[index];
            //-------------------------------//
            //** Turning Speed Adjustment ***//
            //-------------------------------//
            var nextDirection = DirectionType.Up;

            if (edgeExploreResult.IntersectionDistance < UrbanSafetyDistanceInM)
            {
                nextDirection = edgeExploreResult.Edge.To.GetDirection(edgeExploreResult.Edge,
                    exploreResults[index + 1].Edge);
                var turningSpeed = Vehicle.TurningSpeedFor(nextDirection);

                if (turningSpeed > 0)
                {
                    if (Vehicle.Velocity > turningSpeed)
                    {
                        var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed,
                            edgeExploreResult.IntersectionDistance, turningSpeed, 0);

                        //TODO have to check this on semantics
                        if (Vehicle.Velocity + speedChange < turningSpeed)
                        {
                            var a = turningSpeed - Vehicle.Velocity;
                            if (a < biggestDeceleration)
                                biggestDeceleration = a;
                            continue;
                        }

                        if (speedChange < biggestDeceleration)
                            biggestDeceleration = speedChange;
                    }
                    else
                    {
                        var speedChange = CalculateSpeedChange(Vehicle.Velocity,
                            turningSpeed, FreeDrivingClearanceInM, turningSpeed, 0);

                        if (speedChange < biggestDeceleration)
                            biggestDeceleration = speedChange;
                    }
                }
            }

            //-------------------------------//
            //** Traffic Lights Adjustments *//
            //-------------------------------//
            if (edgeExploreResult.LightPhase == TrafficLightPhase.None &&
                edgeExploreResult.IntersectionDistance < UrbanSafetyDistanceInM)
            {
                var intersectionWithMultipleEdges = edgeExploreResult.Edge.To.IncomingEdges.Count > 1;
                if (intersectionWithMultipleEdges)
                {
                    var deceleration = _intersectionTrafficCode.Evaluate(edgeExploreResult, nextDirection);
                    if (deceleration < biggestDeceleration)
                        biggestDeceleration = deceleration;
                }
            }
            else if (edgeExploreResult.LightPhase == TrafficLightPhase.Yellow)
            {
                var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed,
                    edgeExploreResult.IntersectionDistance, 0, 0);

                if (speedChange <= Vehicle.MaxDeceleration && speedChange < biggestDeceleration)
                    biggestDeceleration = speedChange;
            }
            else if (edgeExploreResult.LightPhase == TrafficLightPhase.Red) //TODO check for emergency vehicles in action
            {
                //TODO traffic light controller holen und auf grün schalten
                if (Vehicle.Driver is EmergencyCarDriver)
                {
                    //edgeExploreResult.LightPhase = TrafficLightPhase.Green; 
                    //TODO force green
                    String entities = edgeExploreResult.Edge.Entities.ToString();
                    if (entities.Contains("TrafficLight"))
                    {
                        Console.WriteLine("Traffic Light found");
                        //TODO force green
                    }
                  return biggestDeceleration;  
                }
                
                var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed,
                    edgeExploreResult.IntersectionDistance, 0, 0);
                if (speedChange < biggestDeceleration)
                    biggestDeceleration = speedChange;
            }
        }

        return biggestDeceleration;
    }

    protected virtual double CalculateSpeedChange(double currentSpeed, double maxSpeed,
        double distanceToVehicleAhead,
        double speedVehicleAhead, double accelerationVehicleAhead)
    {
        return VehicleAccelerator.CalculateSpeedChange(currentSpeed, maxSpeed, distanceToVehicleAhead,
            speedVehicleAhead);
    }

    private double HandleVehiclesAhead(SpatialGraphExploreResult exploreResult, double biggestDeceleration)
    {
        if (!Vehicle.IsCollidingEntity) return biggestDeceleration;

        //TODO nicht Spurwechsel, wenn Abbiegevorgang ansteht
        var (entityAhead, distanceToEntityAhead) = FindEntityAhead(exploreResult, Route);
        if (entityAhead == null) return biggestDeceleration;

        var vehicleAhead = entityAhead is RoadUser vehicle ? vehicle : new RoadBlocker(entityAhead);

        var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed, distanceToEntityAhead,
            vehicleAhead.Velocity, vehicleAhead.Acceleration);

        if (!DesireToOvertake(speedChange)) return Math.Min(speedChange, biggestDeceleration);

        var leftIndex = Vehicle.LaneOnCurrentEdge - 1;
        var rightIndex = Vehicle.LaneOnCurrentEdge + 1;

        var edgeExploreResult = exploreResult.EdgeExplores.First();
        var (minLane, maxLane) = edgeExploreResult.Edge.ModalityLaneRanges[Vehicle.ModalityType];
        var hasLeftLane = edgeExploreResult.LaneExplores.ContainsKey(leftIndex) && leftIndex >= minLane &&
                          leftIndex <= maxLane; //TODO test?
        var hasRightLane = edgeExploreResult.LaneExplores.ContainsKey(rightIndex) && rightIndex >= minLane &&
                           rightIndex <= maxLane;

        var (entityAheadLeft, distanceAheadLeft) = FindEntityOnSameEdge(exploreResult, leftIndex, true);
        var (entityAheadRight, distanceAheadRight) = FindEntityOnSameEdge(exploreResult, rightIndex, true);

        var (entityBehindLeft, distanceBehindLeft) = FindEntityOnSameEdge(exploreResult, leftIndex, false);
        var (entityBehindRight, distanceBehindRight) = FindEntityOnSameEdge(exploreResult, rightIndex, false);

        var nextLaneRelative = 0;
        if (hasLeftLane && (entityAheadLeft == null || distanceAheadLeft > distanceToEntityAhead) &&
            OvertakingIsSafelyPossible(entityBehindLeft, distanceBehindLeft))
            nextLaneRelative = -1;

        if (hasRightLane && (entityAheadRight == null || distanceAheadRight > distanceToEntityAhead) &&
            OvertakingIsSafelyPossible(entityBehindRight, distanceBehindRight))
        {
            var switchingLeftNotPossible = nextLaneRelative == 0;
            if (switchingLeftNotPossible || distanceAheadRight > distanceAheadLeft)
                nextLaneRelative = 1;
        }

        if (nextLaneRelative != 0) PlanDesiredLanesForNextMoves(Vehicle.LaneOnCurrentEdge + nextLaneRelative);

        return biggestDeceleration;
    }

    private bool OvertakingIsSafelyPossible(ISpatialGraphEntity entityBehind, double distanceToEntityBehind)
    {
        //TODO also check that not a driver from behind is fast joining up
        if (entityBehind == null) return true;
        var velocity = entityBehind is RoadUser vehicle ? vehicle.Velocity : 0;
        var slower = Velocity > velocity;

        const int safetyTimeInSeconds = 10;
        var speedDifference = Velocity - velocity;
        var safetyDistance = speedDifference * safetyTimeInSeconds;
        if (slower) return distanceToEntityBehind > safetyDistance;

        return distanceToEntityBehind < safetyDistance + SafetyDistanceForOvertaking;
    }

    private bool DesireToOvertake(double speedChange)
    {
        var (minLane, maxLane) = Vehicle.CurrentEdge.ModalityLaneRanges[Vehicle.ModalityType];
        var multiLaneRoad = maxLane - minLane >= 1;
        return Vehicle.Driver.OvertakingActivated && multiLaneRoad && speedChange < 0;
    }

    protected virtual double CalculateDrivingDistance(double biggestDeceleration)
    {
        //TODO magic number, maybe smaller?
        if (Route.RemainingRouteDistanceToGoal < 3) // Make last step to goal
            return Route.RemainingRouteDistanceToGoal;

        if (biggestDeceleration < MaximalDeceleration) // Was speed change set? then use it
            return Vehicle.Velocity + biggestDeceleration;

        // free way driving
        var speedChange = CalculateSpeedChange(Vehicle.Velocity, MaxSpeed,
            FreeDrivingClearanceInM, SpeedLimit, 0);
        return Vehicle.Velocity + speedChange;
    }

    private void PerformMoveAction(double distance)
    {
        if (distance > 0)
        {
            if (Environment.Move(Vehicle, Route, distance))
            {
                Vehicle.Velocity = Math.Round(distance, 2);
                Vehicle.Acceleration = distance - Vehicle.Velocity;
                Vehicle.Position = Vehicle.CalculateNewPositionFor(Route, out var bearing);
                Vehicle.Bearing = bearing;
            }

            if (Vehicle.CurrentEdge != Route.Stops.First().Edge) PlanDesiredLanesForNextMoves();
        }
        else
        {
            Vehicle.Acceleration = -Vehicle.Velocity; //TODO really?
            Vehicle.Velocity = 0;
        }

        if (GoalReached) Vehicle.Velocity = 0;
    }

    /// <summary>
    ///     Tries to find an entity ahead for given route within the explore result.
    /// </summary>
    /// <param name="exploreResult">Contains the exploration of the vehicle.</param>
    /// <param name="route">Holds the lanes for all relevant edges.</param>
    /// <returns>
    ///     The next entity on any of the upcoming lanes and its distance to the calling vehicle.
    ///     Null (and 0 as distance) if no entity could be found within the exploration distance.
    /// </returns>
    public (ISpatialGraphEntity, double) FindEntityAhead(SpatialGraphExploreResult exploreResult, Route route)
    {
        var distance = Vehicle.CurrentEdge != null ? -Vehicle.PositionOnCurrentEdge : 0;
        for (var i = 0; i < exploreResult.EdgeExplores.Count; i++)
        {
            var edgeExplore = exploreResult.EdgeExplores[i];
            var desiredLane = Math.Max(0, route[i].DesiredLane);

            if (edgeExplore.LaneExplores.TryGetValue(desiredLane, out var laneExploreResult))
            {
                var entity = laneExploreResult.Forward.FirstOrDefault();
                if (entity != null)
                {
                    distance += entity.PositionOnCurrentEdge;
                    return (entity, distance);
                }
            }

            distance += edgeExplore.Edge.Length;
        }

        return (null, 0);
    }

    /// <summary>
    ///     Finds an entity and the distance to this vehicle within the current edge on a specific lane if it exists.
    /// </summary>
    /// <param name="exploreResult">Holds exploration data.</param>
    /// <param name="lane">On that the searched entity may be located.</param>
    /// <param name="forward">True, if searching in front, false if searching backwards.</param>
    /// <returns>The found entity and the relative distance to this vehicle. (null, 0) if none is found.</returns>
    public (ISpatialGraphEntity, double) FindEntityOnSameEdge(SpatialGraphExploreResult exploreResult, int lane,
        bool forward)
    {
        var edgeExploreResult = exploreResult.EdgeExplores.FirstOrDefault();
        if (edgeExploreResult == null || !edgeExploreResult.LaneExplores.ContainsKey(lane)) return (null, 0);

        var distance = Vehicle.CurrentEdge != null ? -Vehicle.PositionOnCurrentEdge : 0;
        var laneExplore = edgeExploreResult.LaneExplores[lane];
        var entity = (forward ? laneExplore.Forward : laneExplore.Backward).FirstOrDefault();
        if (entity == null) return (null, 0);

        distance += entity.PositionOnCurrentEdge;
        return (entity, distance);
    } //TODO write test cases


    protected void ChangeLane()
    {
        if (Route.Stops[0].DesiredLane > Vehicle.LaneOnCurrentEdge &&
            ((SpatialEdge)Route.Stops[0].Edge).LaneCount > Route.Stops[0].DesiredLane)
            Route.Stops[0].DesiredLane += 1;

        if (Route.Stops[0].DesiredLane < Vehicle.LaneOnCurrentEdge && Route.Stops[0].DesiredLane >= 0)
            Route.Stops[0].DesiredLane -= 1;

        if (!Environment.Move(Vehicle, Route, 0.1)) Route.Stops[0].DesiredLane = Vehicle.LaneOnCurrentEdge;
    }

    protected bool MoveFromNodeSuccessfully()
    {
        if (Route.Stops.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(Route), "Route was empty");

        PlanDesiredLanesForNextMoves();

        Vehicle.Acceleration = 0.0;

        return Environment.Move(Vehicle, Route, 0.001);
    }

    private void PlanDesiredLanesForNextMoves(int desiredLane = -1)
    {
        if (desiredLane >= 0) Route.First().DesiredLane = desiredLane;

        const int maxLanesAhead = 5;
        var firstLaneInitRequired = desiredLane < 0 && Vehicle.LaneOnCurrentEdge == -1;
        for (var i = firstLaneInitRequired ? 0 : 1;
             i < maxLanesAhead && i < Route.Stops.Count - 1 && ((SpatialEdge)Route.Stops[i].Edge).LaneCount > 1;
             i++)
        {
            var currentStop = Route.Stops[i];
            var (leftLane, rightLane) = currentStop.Edge.ModalityLaneRanges[Vehicle.ModalityType];
            if (currentStop.DesiredLane == -1 || desiredLane != -1)
            {
                var currentLane = desiredLane < 0 ? leftLane : desiredLane;
                var nextStop = Route.Stops[i + 1];
                var incoming = currentStop.Edge.From.Position.GetBearing(currentStop.Edge.To.Position);
                var outgoing = nextStop.Edge.From.Position.GetBearing(nextStop.Edge.To.Position);

                var direction = PositionHelper.GetDirectionType(incoming, outgoing);
                switch (direction)
                {
                    case DirectionType.Up:
                        currentStop.DesiredLane = rightLane > currentLane ? currentLane : rightLane;
                        break;
                    case DirectionType.Left:
                    case DirectionType.UpLeft:
                    case DirectionType.DownLeft:
                        currentStop.DesiredLane = leftLane;
                        break;
                    case DirectionType.Right:
                    case DirectionType.UpRight:
                    case DirectionType.DownRight:
                        currentStop.DesiredLane = rightLane;
                        break;
                    case DirectionType.Down:
                        currentStop.DesiredLane = leftLane;
                        break;
                    default:
                        currentStop.DesiredLane = leftLane;
                        break;
                }
            }
        }
    }
}

internal sealed class RoadBlocker : RoadUser
{
    public RoadBlocker(ISpatialGraphEntity entityAhead)
    {
        Velocity = 0;
        Acceleration = 0;
        CurrentEdge = entityAhead.CurrentEdge;
        PositionOnCurrentEdge = entityAhead.PositionOnCurrentEdge;
        LaneOnCurrentEdge = entityAhead.LaneOnCurrentEdge;
    }
}