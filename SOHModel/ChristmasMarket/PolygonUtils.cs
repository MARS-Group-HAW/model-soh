using System.Globalization;

namespace SOHModel.ChristmasMarket;

public class PolygonUtils
{
    public static List<(double lon, double lat)> ParsePolygon(string topLeft, string topRight, string bottomRight, string bottomLeft)
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

    public static bool IsPointInPolygon(double lon, double lat, List<(double lon, double lat)> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        // Kanten werden in dieser Reihenfolge geprüft:
        // (oben links -> oben rechts), (oben rechts -> unten rechts),
        // (unten rechts -> unten links), (unten links -> oben links)
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

        var s0 = Sign(0, 1);
        var s1 = Sign(1, 2);
        var s2 = Sign(2, 3);
        var s3 = Sign(3, 0);

        bool nonNeg = (s0 >= 0) && (s1 >= 0) && (s2 >= 0) && (s3 >= 0);
        bool nonPos = (s0 <= 0) && (s1 <= 0) && (s2 <= 0) && (s3 <= 0);
        return nonNeg || nonPos;
    }
}