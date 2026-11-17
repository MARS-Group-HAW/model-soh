using Mars.Interfaces.Environments;

namespace SOHModel.SemiTruck.Model.Driver.Utils;

/// <summary>
/// Speeds assumed when planning ahead, all derived from <see cref="SemiTruck.PlanningMaxSpeed"/>.
/// </summary>
internal static class PlanningSpeed
{
    /// <summary>Cautious average speed: legal reach and planning horizons must not be overestimated.</summary>
    public static double Conservative(SemiTruck truck) =>
        Math.Max(1.0, truck.PlanningMaxSpeed * SemiTruckDriverConstants.PlanningSpeedFraction);

    /// <summary>Expected average speed, for comparing the elapsed time of alternatives.</summary>
    public static double Expected(SemiTruck truck) =>
        Math.Max(1.0, truck.PlanningMaxSpeed * SemiTruckDriverConstants.ExpectedSpeedFraction);

    /// <summary>Speed the truck can drive on <paramref name="edge"/>: its own limit capped by the edge's limit.</summary>
    public static double OnEdge(SemiTruck truck, ISpatialEdge edge) =>
        edge.MaxSpeed > 0 ? Math.Min(truck.PlanningMaxSpeed, edge.MaxSpeed) : truck.PlanningMaxSpeed;
}
