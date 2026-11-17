using Mars.Interfaces.Environments;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHModel.SemiTruck.Model.Driver.Services;

/// <summary>Distance and planned driving time of a sequence of edges.</summary>
public readonly record struct RouteEstimate(double Meters, double Seconds)
{
    /// <summary>
    /// Estimates the edges at the conservative planning speed, capped per edge by its limit. The first edge is
    /// counted from <paramref name="positionOnFirstEdge"/>.
    /// </summary>
    public static RouteEstimate Of(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge, SemiTruck truck)
    {
        bool first = true;
        double meters = 0;
        double seconds = 0;
        foreach (var edge in edges)
        {
            double distance = Math.Max(0, edge.Length - (first ? positionOnFirstEdge : 0));
            first = false;
            double speed = PlanningSpeed.OnEdge(truck, edge) * SemiTruckDriverConstants.PlanningSpeedFraction;
            if (speed <= 0) return new RouteEstimate(double.PositiveInfinity, double.PositiveInfinity);
            meters += distance;
            seconds += distance / speed;
        }
        return new RouteEstimate(meters, seconds);
    }
}
