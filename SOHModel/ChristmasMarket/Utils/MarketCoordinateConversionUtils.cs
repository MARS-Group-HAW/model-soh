using Mars.Interfaces.Environments;

namespace SOHModel.ChristmasMarket.Utils;

/// <summary>
/// Provides utility methods for geographic coordinate conversions and calculations,
/// such as calculating destination points based on bearing and distance.
/// </summary>
public class MarketCoordinateConversionUtils
{
    private const double EarthRadiusMeters = 6371000;

    /// <summary>
    /// Converts an angle from degrees to radians.
    /// </summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The equivalent angle in radians.</returns>
    public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Converts an angle from radians to degrees.
    /// </summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The equivalent angle in degrees.</returns>
    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
    
    /// <summary>
    /// Calculates a destination coordinate given a starting point, a bearing, and a distance.
    /// This is useful for moving an agent in a specific direction over a certain distance.
    /// </summary>
    /// <param name="start">The starting position (longitude, latitude).</param>
    /// <param name="bearing">The direction of travel in degrees, where 0 is North, 90 is East, etc.</param>
    /// <param name="distanceMeters">The distance to travel along the bearing in meters.</param>
    /// <returns>The new Position representing the calculated destination.</returns>
    public static Position CalculateDestination(Position start, double bearing, double distanceMeters)
    {
        var angularDistance = distanceMeters / EarthRadiusMeters;
        var bearingRad = ToRadians(bearing);

        var lat1 = ToRadians(start.Y);
        var lon1 = ToRadians(start.X);

        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(angularDistance) +
                             Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearingRad));

        var lon2 = lon1 + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(lat1),
            Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

        return new Position(ToDegrees(lon2), ToDegrees(lat2));
    }
}