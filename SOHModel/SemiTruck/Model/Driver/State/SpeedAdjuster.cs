using System;
using System.Linq;
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
        /// Adjusts speed based on incline of current road segment.
        /// </summary>
        public void AdjustForIncline(ISpatialEdge currentEdge, SemiTruck truck, SemiTruckDriver driver, FuelConsumptionTracker fuelTracker)
        {
            if (currentEdge.Attributes.TryGetValue("incline", out var inclineValue))
            {
                double inclineMagnitude = ParseIncline(inclineValue?.ToString());
                double sign = DetermineElevationSign(currentEdge);
                double incline = inclineMagnitude * sign;
                fuelTracker.UpdateIncline(incline);
                double adjustedSpeed = CalculateMaxSpeedOnIncline(incline, truck, driver.MaxSpeed);

                // Backup original speed once
                if (_originalMaxSpeed < 0)
                    _originalMaxSpeed = truck.MaxSpeed;

                // Reduce speed if necessary
                if (adjustedSpeed < _originalMaxSpeed)
                {
                    driver.MaxSpeed = adjustedSpeed;
                }
                else
                {
                    // Reset speed if incline is not present
                    driver.MaxSpeed = _originalMaxSpeed;
                }
            }
            else
            {
                fuelTracker.UpdateIncline(0);
                // No incline → reset
                if (_originalMaxSpeed > 0)
                {
                    driver.MaxSpeed = _originalMaxSpeed;
                }
            }
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
            double massKg = truck.Mass * 1000.0; // Convert t to kg
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
        /// Parses the magnitude of an incline string attribute from OSM/GeoJSON format.
        /// Sign is determined separately via <see cref="DetermineElevationSign"/>.
        /// </summary>
        private static double ParseIncline(string inclineStr)
        {
            if (string.IsNullOrWhiteSpace(inclineStr))
                return 0.0;

            inclineStr = inclineStr.Trim().ToLower();

            if (inclineStr.EndsWith('%') && double.TryParse(inclineStr.TrimEnd('%'), out double percent))
                return Math.Abs(percent);

            if (inclineStr == "up") return 5.0;
            if (inclineStr == "down") return 5.0;
            if (double.TryParse(inclineStr, out double value)) return Math.Abs(value);

            return 0.0;
        }

        /// <summary>
        /// Determines the sign of an edge's incline (+1 uphill, -1 downhill) by comparing the
        /// first and last values of the edge's elevation array attribute.
        /// Travel always proceeds from <c>edge.From</c> to <c>edge.To</c>, which corresponds to
        /// the first and last entries in the elevation profile.
        /// Returns +1 when elevation data is unavailable or flat.
        /// </summary>
        private static double DetermineElevationSign(ISpatialEdge edge)
        {
            if (!edge.Attributes.TryGetValue("elevation", out var elevationValue) || elevationValue == null)
                return 1.0;

            var elevationStr = elevationValue.ToString()?.Trim();
            if (string.IsNullOrEmpty(elevationStr))
                return 1.0;

            // elevation is stored as a JSON array string: "[724.5,723.1,...]"
            if (elevationStr.StartsWith('[') && elevationStr.EndsWith(']'))
            {
                var inner = elevationStr.Substring(1, elevationStr.Length - 2);
                var parts = inner.Split(',');
                if (parts.Length >= 2
                    && double.TryParse(parts[0].Trim(), out double first)
                    && double.TryParse(parts[^1].Trim(), out double last))
                {
                    return last >= first ? 1.0 : -1.0;
                }
            }
            
            return 1.0;
        }
    }
}
