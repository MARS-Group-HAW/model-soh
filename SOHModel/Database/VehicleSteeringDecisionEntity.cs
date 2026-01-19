namespace SOHModel.Database;

/// <summary>
/// Captures all decision-making factors during VehicleSteeringHandle.Move() execution.
/// Used for debugging and analyzing driver behavior, traffic rules, and vehicle interactions.
/// </summary>
public record VehicleSteeringDecisionEntity(
    // Identification
    Guid AgentId,
    long Tick,
    double Latitude,
    double Longitude,

    // Speed Constraints
    double SpeedLimit,
    double MaxSpeed,
    double CurrentVelocity,

    // Braking Decision
    bool BrakingActivated,
    double? BrakingDeceleration,

    // Intersection Ahead Data
    double? IntersectionDistance,
    string? IntersectionDirection,
    string? TrafficLightPhase,
    double? TurningSpeedRequired,
    double? IntersectionDeceleration,
    bool? ApproachingEndOfRoute,

    // Traffic Code / Intersection Rules
    string? TrafficCodeType,
    double? TrafficCodeDeceleration,

    // Vehicle Ahead Data
    bool HasVehicleAhead,
    double? DistanceToVehicleAhead,
    double? VehicleAheadSpeed,
    double? VehicleAheadAcceleration,
    double? VehicleAheadDeceleration,
    bool? IsVehicleAheadRoadBlocker,

    // Overtaking Decision
    bool DesireToOvertake,
    bool? MultiLaneRoad,
    bool? HasLeftLane,
    bool? HasRightLane,
    double? DistanceToVehicleAheadLeft,
    double? DistanceToVehicleAheadRight,
    double? DistanceToBehindLeft,
    double? DistanceToBehindRight,
    bool? LeftLaneSafe,
    bool? RightLaneSafe,
    int? SelectedLaneChange, // -1 for left, 0 for no change, 1 for right

    // Final Decision
    double BiggestDeceleration,
    double CalculatedDrivingDistance,

    // Environment
    double RemainingRouteDistance,
    int? CurrentLane,
    string? CurrentEdgeId
);
