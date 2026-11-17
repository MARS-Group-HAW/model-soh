using Mars.Interfaces.Environments;
using SOHModel.SemiTruck.Model.Driver.State;

namespace SOHModel.SemiTruck.Model.Driver.Services;

internal static class StopReachability
{
    /// <summary>
    /// True if the truck can drive the edges within the legal driving time left (in whole ticks) and with the
    /// energy on board (see <see cref="FuelConsumptionTracker.CanCover"/>).
    /// </summary>
    public static bool CanReach(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
        SemiTruck truck, FuelConsumptionTracker fuel, DrivingTimeTracker drivingTime, TimeSpan tickDuration)
    {
        var list = edges as IReadOnlyCollection<ISpatialEdge> ?? edges.ToList();
        double seconds = RouteEstimate.Of(list, positionOnFirstEdge, truck).Seconds;

        // Include tick quantization so a stop never relies on a fractional final driving tick.
        double tickSeconds = tickDuration.TotalSeconds;
        if (tickSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(tickDuration));
        seconds = Math.Ceiling(seconds / tickSeconds) * tickSeconds;
        return seconds <= drivingTime.LegalDrivingLeft.TotalSeconds &&
               fuel.CanCover(list, positionOnFirstEdge, truck);
    }
}
