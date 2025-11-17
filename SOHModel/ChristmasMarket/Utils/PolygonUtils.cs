using System.Globalization;

namespace SOHModel.ChristmasMarket.Utils;

/// <summary>
/// Provides utility methods for handling polygon geometry for market areas.
/// </summary>
public class PolygonUtils
{
    /// <summary>
    /// Parses four corner strings into a list of coordinate tuples representing a polygon.
    /// </summary>
    /// <param name="topLeft">The top left corner in "longitude, latitude" format.</param>
    /// <param name="topRight">The top right corner in "longitude, latitude" format.</param>
    /// <param name="bottomRight">The bottom right corner in "longitude, latitude" format.</param>
    /// <param name="bottomLeft">The bottom left corner in "longitude, latitude" format.</param>
    /// <returns>A list of (lon, lat) tuples on success, or null if the input is invalid.</returns>
    public static List<(double lon, double lat)> ParsePolygon(string topLeft, string topRight, string bottomRight,
        string bottomLeft)
    {
        var corners = new[] { topLeft, topRight, bottomRight, bottomLeft };
        var pts = new List<(double lon, double lat)>(4);

        foreach (var corner in corners)
        {
            if (string.IsNullOrWhiteSpace(corner)) return null;
            var parts = corner.Split(',');
            if (parts.Length < 2) return null;

            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            {
                return null;
            }

            pts.Add((lon, lat));
        }

        return pts;
    }

    /// <summary>
    /// Determines if a given point is inside of a convex polygon.
    /// </summary>
    /// <param name="lon">The longitude of the point to check.</param>
    /// <param name="lat">The latitude of the point to check.</param>
    /// <param name="polygon">A list of (lon, lat) tuples.</param>
    /// <returns>True if the point is inside the polygon, false it is not.</returns>
    public static bool IsPointInPolygon(double lon, double lat, List<(double lon, double lat)> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        double Sign(int i1, int i2)
        {
            var (x1, y1) = polygon[i1];
            var (x2, y2) = polygon[i2];
            var vx = x2 - x1;
            var vy = y2 - y1;
            var wx = lon - x1;
            var wy = lat - y1;
            return vx * wy - vy * wx;
        }

        // Edges are checked in order: (TL -> TR), (TR -> BR), (BR -> BL), (BL -> TL)
        var s0 = Sign(0, 1);
        var s1 = Sign(1, 2);
        var s2 = Sign(2, 3);
        var s3 = Sign(3, 0);

        bool nonNeg = (s0 >= 0) && (s1 >= 0) && (s2 >= 0) && (s3 >= 0);
        bool nonPos = (s0 <= 0) && (s1 <= 0) && (s2 <= 0) && (s3 <= 0);
        return nonNeg || nonPos;
    }
}