using System.Globalization;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.RealTimeData;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages speed adjustments based on incline, weather, and road attributes.
    /// </summary>
    public class SpeedAdjuster
    {
        private double _originalMaxSpeed = -1;

        /// <summary>
        /// Adjusts speed based on weather conditions.
        /// </summary>
        public void AdjustForWeather(SemiTruckDriver driver, SemiTruckWeatherLayer weatherLayer)
        {
            if (weatherLayer == null || driver.Position == null)
                return;

            var point = new Point(driver.Longitude, driver.Latitude);
            var now = weatherLayer.SemiTruckLayer.Context.CurrentTimePoint ?? DateTime.Now;

            // Find weather zone that contains the truck's current position
            var affectedZone = weatherLayer.AllZones
                .FirstOrDefault(z =>
                    z.Area.Contains(point) &&
                    z.SpeedFactor < 1.0 &&
                    z.StartTime <= now &&
                    z.EndTime >= now);

            // Store original speed once for reset
            if (_originalMaxSpeed < 0)
                _originalMaxSpeed = driver.SemiTruck.MaxSpeed;

            // Reset accident rate (in case previously modified)
            driver.SemiTruck.AccidentsPerYear = driver.DefaultAccidentsPerYear;

            if (affectedZone != null)
            {
                driver.MaxSpeed = _originalMaxSpeed * affectedZone.SpeedFactor;

                // Adjust accident risk if conditions are snowy or severely slowed
                if (affectedZone.Type?.ToLower().Contains("schnee") == true ||
                    affectedZone.SpeedFactor <= 0.6)
                {
                    driver.SemiTruck.AccidentsPerYear *= SemiTruckDriverConstants.SnowWeatherAccidentMultiplier;
                }
            }
            else
            {
                driver.MaxSpeed = _originalMaxSpeed;
            }
        }

        /// <summary>
        /// Adjusts speed based on the local grade at the truck's position on the current edge.
        /// The grade is derived from the edge's <c>elevation</c> profile (see <see cref="ComputeGradePercent"/>);
        /// the raw <c>incline</c> tag is only a fallback for edges without elevation data.
        /// </summary>
        public void AdjustForIncline(ISpatialEdge currentEdge, SemiTruck truck, SemiTruckDriver driver, FuelConsumptionTracker fuelTracker)
        {
            double? incline = GetGradeFromElevation(currentEdge, truck.PositionOnCurrentEdge);

            if (incline == null && currentEdge.Attributes.TryGetValue("incline", out var inclineValue))
                incline = ParseIncline(inclineValue?.ToString());

            if (incline == null)
            {
                fuelTracker.UpdateIncline(0);
                if (_originalMaxSpeed > 0)
                    driver.MaxSpeed = _originalMaxSpeed;
                return;
            }

            fuelTracker.UpdateIncline(incline.Value);
            double adjustedSpeed = CalculateMaxSpeedOnIncline(incline.Value, truck, driver.MaxSpeed);

            // Backup original speed once
            if (_originalMaxSpeed < 0)
                _originalMaxSpeed = truck.MaxSpeed;

            driver.MaxSpeed = adjustedSpeed < _originalMaxSpeed ? adjustedSpeed : _originalMaxSpeed;
        }

        private readonly Dictionary<ISpatialEdge, double[]> _elevationCache = new();

        private double? GetGradeFromElevation(ISpatialEdge edge, double positionOnEdge)
        {
            if (!_elevationCache.TryGetValue(edge, out var profile))
            {
                profile = ParseElevationProfile(edge);
                _elevationCache[edge] = profile;
            }

            return profile == null ? null : ComputeGradePercent(profile, edge.Length, positionOnEdge);
        }

        /// <summary>
        /// Computes the signed grade (percent, positive = uphill in travel direction From→To) at a position
        /// along an edge from its equally spaced elevation samples. The grade is averaged over a window of
        /// <see cref="SemiTruckDriverConstants.GradeSmoothingWindowMeters"/> centred on the position
        /// (shifted at the edge ends) to suppress elevation-model noise, and clamped to
        /// ±<see cref="SemiTruckDriverConstants.MaxPlausibleGradePercent"/>.
        /// </summary>
        internal static double ComputeGradePercent(double[] elevations, double edgeLengthMeters, double positionOnEdge)
        {
            if (elevations.Length < 2 || edgeLengthMeters <= 0)
                return 0.0;

            double window = Math.Min(SemiTruckDriverConstants.GradeSmoothingWindowMeters, edgeLengthMeters);
            double start = Math.Clamp(positionOnEdge - window / 2, 0, edgeLengthMeters - window);
            double end = start + window;

            double grade = (ElevationAt(elevations, edgeLengthMeters, end) - ElevationAt(elevations, edgeLengthMeters, start))
                           / window * 100.0;
            double max = SemiTruckDriverConstants.MaxPlausibleGradePercent;
            return Math.Clamp(grade, -max, max);
        }

        private static double ElevationAt(double[] elevations, double edgeLengthMeters, double position)
        {
            double idx = position / edgeLengthMeters * (elevations.Length - 1);
            int lo = Math.Clamp((int)Math.Floor(idx), 0, elevations.Length - 1);
            int hi = Math.Min(lo + 1, elevations.Length - 1);
            return elevations[lo] + (elevations[hi] - elevations[lo]) * (idx - lo);
        }

        /// <summary>
        /// Parses the <c>elevation</c> edge attribute, a JSON array string such as "[724.5,723.1,...]".
        /// Returns null if missing, malformed or containing null entries.
        /// </summary>
        internal static double[] ParseElevationProfile(ISpatialEdge edge)
        {
            if (!edge.Attributes.TryGetValue("elevation", out var value) || value == null)
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

        /// <summary>
        /// Updates overtaking permission based on road attributes.
        /// </summary>
        public void UpdateOvertaking(ISpatialEdge currentEdge, SemiTruckDriver driver)
        {
            if (currentEdge.Attributes.TryGetValue("overtaking", out var overtakingValue))
            {
                driver.OvertakingActivated = overtakingValue?.ToString()?.ToLower() == "yes";
            }
        }

        /// <summary>
        /// Calculates the maximum feasible speed on a given incline.
        /// </summary>
        private double CalculateMaxSpeedOnIncline(double inclinePercent, SemiTruck truck, double currentMaxSpeed)
        {
            double powerWatt = truck.Power * 1000.0; // Convert kW to W
            double massKg = truck.Mass; // kg
            const double minSpeedMps = SemiTruckDriverConstants.MinSpeedKmh / 3.6; // km/h → m/s

            if (inclinePercent <= 0.0)
                return currentMaxSpeed; // no incline → full speed

            double denominator = massKg * SemiTruckDriverConstants.GravityConstant * (inclinePercent / 100.0);
            if (denominator == 0) return truck.MaxSpeed;

            double vMps = powerWatt / denominator;

            // Clamp result between minimum required speed and current truck max
            return Math.Max(minSpeedMps, Math.Min(vMps, currentMaxSpeed));
        }

        /// <summary>
        /// Parses the magnitude of the OSM <c>incline</c> tag. Fallback only: the tag carries no reliable
        /// sign or size (often placeholders such as 10 or 26), so it is used when no elevation profile exists.
        /// </summary>
        private static double ParseIncline(string inclineStr)
        {
            if (string.IsNullOrWhiteSpace(inclineStr))
                return 0.0;

            inclineStr = inclineStr.Trim().ToLower();

            if (inclineStr.EndsWith('%') && double.TryParse(inclineStr.TrimEnd('%'), out double percent))
                return Math.Abs(percent);

            if (inclineStr == "up") return 5.0;
            if (inclineStr == "down") return -5.0;
            if (double.TryParse(inclineStr, out double value)) return Math.Abs(value);

            return 0.0;
        }
    }
}
