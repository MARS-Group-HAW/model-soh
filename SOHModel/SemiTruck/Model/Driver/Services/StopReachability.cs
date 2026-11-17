using System.Runtime.CompilerServices;
using Mars.Interfaces.Environments;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHModel.SemiTruck.Model.Driver.Services;

internal static class StopReachability
{
    public static double EstimateSeconds(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
        SemiTruck truck) => Estimate(edges, positionOnFirstEdge, truck).Seconds;

    private static (double Meters, double Seconds) Estimate(IEnumerable<ISpatialEdge> edges,
        double positionOnFirstEdge, SemiTruck truck)
    {
        bool first = true;
        double meters = 0;
        double seconds = 0;
        foreach (var edge in edges)
        {
            double distance = Math.Max(0, edge.Length - (first ? positionOnFirstEdge : 0));
            first = false;
            double speed = edge.MaxSpeed > 0 ? Math.Min(truck.PlanningMaxSpeed, edge.MaxSpeed) : truck.PlanningMaxSpeed;
            speed *= SemiTruckDriverConstants.PlanningSpeedFraction;
            if (speed <= 0) return (double.PositiveInfinity, double.PositiveInfinity);
            meters += distance;
            seconds += distance / speed;
        }
        return (meters, seconds);
    }

    public static bool CanReach(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
        SemiTruck truck, FuelConsumptionTracker fuel, DrivingTimeTracker drivingTime, TimeSpan tickDuration)
    {
        var (meters, seconds) = Estimate(edges, positionOnFirstEdge, truck);

        // Include tick quantization so a stop never relies on a fractional final driving tick.
        double tickSeconds = tickDuration.TotalSeconds;
        if (tickSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(tickDuration));
        seconds = Math.Ceiling(seconds / tickSeconds) * tickSeconds;
        return seconds <= drivingTime.LegalDrivingLeft.TotalSeconds &&
               meters <= fuel.PlanningRangeKm(truck) * 1000 &&
               RequiredWheelJoules(edges, positionOnFirstEdge, truck, fuel) <= fuel.Store.WheelJoulesAvailable;
    }

    private static readonly ConditionalWeakTable<ISpatialEdge, double[]> Profiles = new();

    /// <summary>
    /// Wheel energy needed to drive the edges: the planning consumption per distance plus the potential energy of
    /// the elevation gained (descents are credited only partially). Taken as the maximum over every elevation
    /// sample, so the truck must also be able to reach the highest point on the way.
    /// </summary>
    internal static double RequiredWheelJoules(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
        SemiTruck truck, FuelConsumptionTracker fuel)
    {
        double perMeter = fuel.PlanningWheelJoulesPerKm(truck) / 1000.0;
        double weight = Math.Max(0, truck.Mass) * 9.81;
        double along = 0;
        double potential = 0;
        double worst = 0;
        bool first = true;
        foreach (var edge in edges)
        {
            double start = first ? Math.Clamp(positionOnFirstEdge, 0, edge.Length) : 0;
            first = false;
            var profile = Profiles.GetValue(edge, e => SpeedAdjuster.ParseElevationProfile(e) ?? Array.Empty<double>());
            if (profile.Length < 2 || edge.Length <= 0)
            {
                along += edge.Length - start;
                worst = Math.Max(worst, perMeter * along + potential);
                continue;
            }

            double step = edge.Length / (profile.Length - 1);
            double previousX = start;
            double previousH = ElevationAt(profile, step, start);
            for (int i = (int)Math.Floor(start / step) + 1; i < profile.Length; i++)
            {
                double x = i * step;
                double rise = profile[i] - previousH;
                along += x - previousX;
                potential += weight * (rise >= 0 ? rise : rise * SemiTruckDriverConstants.DescentRecoveryFactor);
                worst = Math.Max(worst, perMeter * along + potential);
                previousX = x;
                previousH = profile[i];
            }
        }
        return worst;
    }

    private static double ElevationAt(double[] profile, double step, double x)
    {
        int lo = Math.Clamp((int)Math.Floor(x / step), 0, profile.Length - 1);
        int hi = Math.Min(lo + 1, profile.Length - 1);
        return profile[lo] + (profile[hi] - profile[lo]) * (x / step - lo);
    }
}
