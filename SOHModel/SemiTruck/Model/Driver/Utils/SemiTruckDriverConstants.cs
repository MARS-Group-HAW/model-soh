using System;

namespace SOHModel.SemiTruck.Model.Driver.Utils
{
    /// <summary>
    /// Centralized constants for SemiTruckDriver behavior.
    /// </summary>
    public static class SemiTruckDriverConstants
    {
        // Fuel/Energy thresholds
        public const double LowFuelThreshold = 100; // km

        // Search radii for finding stops
        public const double RestAreaSearchRadius = 100_000; // 100 km in meters
        public const double RefuelStationSearchRadius = 100_000; // 100 km in meters
        public const double LookaheadDistance = 5_000; // 5 km in meters

        // Speed calculations
        public const double MinSpeedKmh = 30.0; // Minimum speed on inclines
        public const double GravityConstant = 9.81; // m/s²

        // Spatial calculations
        public const double MetersPerDegree = 111_000; // Approximate meters per degree
        public const double NodeToleranceMeters = 1.0; // Tolerance for node arrival detection

        // Time limits and durations
        public static readonly TimeSpan DefaultRestDuration = TimeSpan.FromHours(4);
        public static readonly TimeSpan MaxDrivingTimeLimit = TimeSpan.FromHours(9);
        public static readonly TimeSpan DefaultAccidentDuration = TimeSpan.FromMinutes(41);
        public static readonly TimeSpan ShoulderAccidentDuration = TimeSpan.FromMinutes(2);

        // Accident probability scaling
        public const double AccidentTruckScalingBase = 650000.0; // Base truck count for scaling
        public const double SeccondsPerYear = 365.0 * 24 * 60 * 60;
        public const double SnowWeatherAccidentMultiplier = 2.06;
    }
}
