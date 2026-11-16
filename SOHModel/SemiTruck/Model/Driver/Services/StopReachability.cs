using Mars.Interfaces.Environments;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHModel.SemiTruck.Model.Driver.Services;

internal static class StopReachability
{
    public static bool CanReach(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
        SemiTruck truck, FuelConsumptionTracker fuel, DrivingTimeTracker drivingTime, TimeSpan tickDuration)
    {
        double meters = 0;
        double seconds = 0;
        bool first = true;
        foreach (var edge in edges)
        {
            double distance = Math.Max(0, edge.Length - (first ? positionOnFirstEdge : 0));
            first = false;
            double speed = edge.MaxSpeed > 0 ? Math.Min(truck.MaxSpeed, edge.MaxSpeed) : truck.MaxSpeed;
            speed *= SemiTruckDriverConstants.PlanningSpeedFraction;
            if (speed <= 0) return false;
            meters += distance;
            seconds += distance / speed;
        }

        // Include tick quantization so a stop never relies on a fractional final driving tick.
        double tickSeconds = tickDuration.TotalSeconds;
        if (tickSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(tickDuration));
        seconds = Math.Ceiling(seconds / tickSeconds) * tickSeconds;
        return seconds <= drivingTime.LegalDrivingLeft.TotalSeconds &&
               meters <= fuel.PlanningRangeKm(truck) * 1000;
    }
}
