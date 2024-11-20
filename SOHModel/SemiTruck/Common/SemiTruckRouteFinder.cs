using Mars.Interfaces.Environments;
using System;
using System.Linq;

namespace SOHModel.SemiTruck.Common
{
    /// <summary>
    ///     Encapsulates the route-finding logic for a semi-truck.
    /// </summary>
    public static class SemiTruckRouteFinder
    {
        private static readonly Random Random = new();

        /// <summary>
        ///     Finds a route for the truck based on the specified drive mode and additional parameters.
        ///     If driveMode is not provided, it defaults to finding the shortest route.
        /// </summary>
        public static Route Find(
            ISpatialGraphEnvironment environment,
            double startLat, double startLon, double destLat, double destLon,
            ISpatialEdge startingEdge, int driveMode = 3, string osmRoute = "")
        {
            Route route = null;
            ISpatialNode currentNode;

            switch (driveMode)
            {
                case 1: // Random short route for testing or urban driving
                {
                    while (route == null)
                    {
                        currentNode = environment.GetRandomNode();
                        var firstEdge = currentNode.OutgoingEdges.Values.FirstOrDefault();
                        if (firstEdge == null) continue;

                        route = new Route { firstEdge };

                        for (var i = 0; i < 5; i++)
                        {
                            var lastEdge = route.Last();
                            var outgoingEdges = lastEdge.Edge.To.OutgoingEdges;
                            if (outgoingEdges.Count == 0) break;

                            var randomIndex = Random.Next(0, outgoingEdges.Count);
                            var nextEdge = outgoingEdges.Values.ElementAt(randomIndex);
                            route.Add(nextEdge);
                        }
                    }

                    break;
                }
                case 2: // Find a route between two random nodes
                {
                    while (route == null)
                    {
                        currentNode = environment.GetRandomNode();
                        var goalNode = environment.GetRandomNode();
                        if (goalNode == null || goalNode.Equals(currentNode)) continue;

                        route = environment.FindRoute(currentNode, goalNode);
                    }

                    break;
                }
                
                // TODO IMPORTANT: Change SpatialModalityType.CarDriving to TruckDriving if available!
                
                case 3: // Find the shortest route from start to destination coordinates (default)
                {
                    currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                    var goalNode = environment.NearestNode(Position.CreateGeoPosition(destLon, destLat));

                    route = environment.FindShortestRoute(currentNode, goalNode,
                        edge => edge.Modalities.Contains(SpatialModalityType.CarDriving)) ?? new Route();

                    break;
                }
                case 4: // Random route starting from a specific location
                {
                    currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                    route = new Route();

                    ISpatialNode goalNode = null;
                    while (route.Count == 0 || goalNode.Equals(currentNode))
                    {
                        goalNode = environment.GetRandomNode();
                        route = environment.FindRoute(currentNode, goalNode);
                    }

                    break;
                }
                case 5: // Continue along a specified starting edge
                {
                    currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                    route = new Route { startingEdge };

                    while (route.Count == 1)
                    {
                        var goalNode = environment.GetRandomNode();
                        var nextEdges = environment.FindRoute(startingEdge.To, goalNode, (_, edge, _) => edge.Length);

                        if (nextEdges != null && !goalNode.Equals(currentNode))
                        {
                            foreach (var edge in nextEdges)
                                route.Add(edge.Edge);
                        }
                    }

                    break;
                }
                case 6: // Follow a specific OSM route sequence
                {
                    currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                    route = new Route();

                    var osmIds = osmRoute.Replace("[", "").Replace("]", "").Split(';');
                    var nodeToTraverse = currentNode;

                    foreach (var osmId in osmIds)
                    {
                        var matchingEdge = nodeToTraverse.OutgoingEdges.Values
                            .FirstOrDefault(edge => edge.Attributes["osmid"].Equals(osmId));

                        if (matchingEdge != null)
                        {
                            route.Add(matchingEdge);
                            nodeToTraverse = matchingEdge.To;
                        }
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(driveMode), "Invalid drive mode for route finding.");
            }

            return route ?? new Route();
        }
    }
}
