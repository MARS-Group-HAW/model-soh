namespace SOHModel.Database;

/// <summary>
/// Captures the physical state and changes during VehicleSteeringHandle.Move() execution.
/// Used for analyzing vehicle dynamics, velocity changes, and spatial movement.
/// </summary>
public record VehicleSteeringPhysicsEntity(
    // Identification
    Guid AgentId,
    long Tick,

    // Position Data
    double Latitude,
    double Longitude,
    double Bearing,

    // Edge/Lane Information
    string? CurrentEdgeId,
    int CurrentLane,
    double PositionOnEdge,
    double RemainingDistanceOnEdge,

    // Velocity & Acceleration
    double VelocityBefore,
    double VelocityAfter,
    double Acceleration,
    double AccelerationApplied,

    // Movement Calculation
    double CalculatedDrivingDistance,
    double ActualDistanceMoved,

    // Route Information
    double RemainingRouteDistance,
    bool GoalReached,

    // Vehicle Properties
    double MaxSpeed,
    double MaxAcceleration,
    double MaxDeceleration,

    // Explore Distance
    double ExploreDistance
);
