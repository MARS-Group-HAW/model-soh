using System.Globalization;
using System.Runtime.CompilerServices;
using Mars.Interfaces.Environments;

namespace SOHModel.SemiTruck.Model.Driver.Utils;

/// <summary>
/// Elevation samples of an edge (attribute <c>elevation</c>, equally spaced from <c>From</c> to <c>To</c>), shared by
/// the driving grade (<see cref="State.SpeedAdjuster"/>) and the energy planning (<see cref="State.FuelConsumptionTracker"/>).
/// </summary>
internal static class ElevationProfile
{
    private static readonly ConditionalWeakTable<ISpatialEdge, double[]> Cache = new();

    /// <summary>The cached profile of <paramref name="edge"/>; <c>null</c> if it has no usable elevation data.</summary>
    public static double[]? Of(ISpatialEdge edge)
    {
        var profile = Cache.GetValue(edge, e => Parse(e) ?? Array.Empty<double>());
        return profile.Length < 2 ? null : profile;
    }

    /// <summary>Linearly interpolated elevation at <paramref name="position"/> metres along the edge.</summary>
    public static double At(double[] profile, double edgeLengthMeters, double position)
    {
        double index = position / edgeLengthMeters * (profile.Length - 1);
        int lo = Math.Clamp((int)Math.Floor(index), 0, profile.Length - 1);
        int hi = Math.Min(lo + 1, profile.Length - 1);
        return profile[lo] + (profile[hi] - profile[lo]) * (index - lo);
    }

    /// <summary>
    /// Parses the <c>elevation</c> edge attribute, a JSON array string such as "[724.5,723.1,...]".
    /// Returns null if missing, malformed or containing null entries.
    /// </summary>
    internal static double[]? Parse(ISpatialEdge edge)
    {
        if (edge.Attributes == null || !edge.Attributes.TryGetValue("elevation", out var value) || value == null)
            return null;

        var str = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(str) || !str.StartsWith('[') || !str.EndsWith(']'))
            return null;

        var parts = str[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var result = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                return null;
        }

        return result;
    }
}
