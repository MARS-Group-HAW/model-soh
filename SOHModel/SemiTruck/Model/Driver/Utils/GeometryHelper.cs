using System;
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
