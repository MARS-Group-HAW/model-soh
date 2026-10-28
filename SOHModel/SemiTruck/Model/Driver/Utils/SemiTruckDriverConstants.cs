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
        public const double MaxFacilityDetourMeters = 10_000; // farther off the route, a rest is taken at the roadside
        public const double RoadsideStopLookaheadMeters = 5_000; // distance ahead at which a roadside stop is placed
        public const double MinObservedDistanceKm = 20; // distance after which the observed consumption is trusted
        public const double RouteChargeReserveKm = 50; // range that should remain on arrival when charging for the route
        public const double RouteChargeSafetyFactor = 1.1; // safety margin on the energy needed for the remaining route

        // Search radii for finding stops
        public const double RestAreaSearchRadius = 100_000; // 100 km in meters
        public const double RefuelStationSearchRadius = 100_000; // 100 km in meters
        public const double LookaheadDistance = 5_000; // 5 km in meters

        // Speed calculations
        public const double MinSpeedKmh = 30.0; // Minimum speed on inclines
        public const double GradeSmoothingWindowMeters = 1000.0; // Window over which the elevation grade is averaged
        public const double MaxPlausibleGradePercent = 6.0; // Clamp for noisy elevation data
        public const double GravityConstant = 9.81; // m/s²

        // Spatial calculations
        public const double MetersPerDegree = 111_000; // Approximate meters per degree
        public const double NodeToleranceMeters = 1.0; // Tolerance for node arrival detection

        // Time limits and durations
        // EU Regulation 561/2006 (legal minimum): 45 min break (15 + 30 min) after 4.5 h, daily rest (reduced) after 9 h
        public static readonly TimeSpan MaxDrivingBeforeBreak = TimeSpan.FromHours(4.5);
        public static readonly TimeSpan BreakDuration = TimeSpan.FromMinutes(45);
        public static readonly TimeSpan BreakFirstPart = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan BreakSecondPart = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan MaxDailyDriving = TimeSpan.FromHours(9);
        public static readonly TimeSpan DailyRestDuration = TimeSpan.FromHours(9);
        public static readonly TimeSpan DefaultRestPlanningMargin = TimeSpan.FromMinutes(30);
        public const double PlanningSpeedFraction = 0.7; // share of max speed assumed to estimate the remaining drive time
        public static readonly TimeSpan DefaultAccidentDuration = TimeSpan.FromMinutes(41);
        public static readonly TimeSpan ShoulderAccidentDuration = TimeSpan.FromMinutes(2);

        // Accident probability scaling
        public const double AccidentTruckScalingBase = 650000.0; // Base truck count for scaling
        public const double SeccondsPerYear = 365.0 * 24 * 60 * 60;
        public const double SnowWeatherAccidentMultiplier = 2.06;
    }
}
