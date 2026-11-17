using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;

namespace SOHModel.SemiTruck.Model.Driver.Utils
{
    /// <summary>
    /// Helper class for spatial geometry calculations.
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// Computes squared Euclidean distance between two coordinates.
        /// Used for nearest neighbor logic (avoids expensive sqrt operation).
        /// </summary>
        /// <param name="lat1">Latitude of first point</param>
        /// <param name="lon1">Longitude of first point</param>
        /// <param name="lat2">Latitude of second point</param>
        /// <param name="lon2">Longitude of second point</param>
        /// <returns>Squared distance</returns>
        public static double GetSquaredDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = lat1 - lat2;
            double dLon = lon1 - lon2;
            return dLat * dLat + dLon * dLon;
        }

        /// <summary>Approximate (equirectangular) distance in meters; sufficient for ranking nearby points.</summary>
        public static double ApproxDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double metersPerDegree = 111_320.0;
            double dLat = (lat1 - lat2) * metersPerDegree;
            double dLon = (lon1 - lon2) * metersPerDegree * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180.0);
            return Math.Sqrt(dLat * dLat + dLon * dLon);
        }

        /// <summary>
        /// Approximate distance in meters from a point to a polyline, and the fraction of the polyline length at
        /// which the closest point lies.
        /// </summary>
        public static (double Meters, double Fraction) DistanceToPolyline(double lat, double lon,
            IReadOnlyList<Position> line)
        {
            if (line.Count == 0) return (double.PositiveInfinity, 0);
            double cos = Math.Cos(lat * Math.PI / 180.0);
            const double m = 111_320.0;
            double best = double.PositiveInfinity, bestAlong = 0, total = 0;
            for (int i = 0; i < line.Count; i++)
            {
                double ax = (line[i].Longitude - lon) * m * cos, ay = (line[i].Latitude - lat) * m;
                if (i == line.Count - 1)
                {
                    double d = Math.Sqrt(ax * ax + ay * ay);
                    if (d < best) { best = d; bestAlong = total; }
                    break;
                }
                double bx = (line[i + 1].Longitude - lon) * m * cos, by = (line[i + 1].Latitude - lat) * m;
                double sx = bx - ax, sy = by - ay, len2 = sx * sx + sy * sy;
                double t = len2 > 0 ? Math.Clamp(-(ax * sx + ay * sy) / len2, 0, 1) : 0;
                double px = ax + t * sx, py = ay + t * sy, dist = Math.Sqrt(px * px + py * py);
                double len = Math.Sqrt(len2);
                if (dist < best) { best = dist; bestAlong = total + t * len; }
                total += len;
            }
            return (best, total > 0 ? bestAlong / total : 0);
        }

        /// <summary>
        /// Checks whether a position is on top of a given node (with tolerance).
        /// </summary>
        /// <param name="currentPosition">Current position to check</param>
        /// <param name="node">Target node</param>
        /// <param name="toleranceMeters">Tolerance in meters (default 1m)</param>
        /// <returns>True if position is within tolerance of node</returns>
        public static bool IsOnNode(Position currentPosition, ISpatialNode node, double toleranceMeters = SemiTruckDriverConstants.NodeToleranceMeters)
        {
            var nodePos = node.Position;

            double dx = currentPosition.Latitude - nodePos.Latitude;
            double dy = currentPosition.Longitude - nodePos.Longitude;
            double distance = Math.Sqrt(dx * dx + dy * dy) * SemiTruckDriverConstants.MetersPerDegree;

            return distance < toleranceMeters;
        }
    }
}
