namespace SOHModel.SemiTruck.Common;

using Mars.Interfaces.Environments;

/// <summary>
///     Provides route-finding functionality for SemiTruck drivers.
/// </summary>
public static class SemiTruckRouteFinder
{
    private static readonly Random Random = new();

    /// <summary>
    ///     Finds a route based on the specified driveMode and parameters.
    /// </summary>
    public static Route Find(ISpatialGraphEnvironment environment, int driveMode,
        double startLat, double startLon, double destLat, double destLon,
        ISpatialEdge startingEdge, string osmRoute)
    {
        Route route = null;
        ISpatialNode currentNode;

        switch (driveMode)
        {
            case 1:
            {
                // Random node traversal for up to 5 edges
                while (route == null)
                {
                    currentNode = environment.GetRandomNode();
                    var firstEdge = currentNode.OutgoingEdges.Values.FirstOrDefault();
                    if (firstEdge == null) continue;

                    route = new Route { firstEdge };

                    // Add up to 5 edges to the route
                    for (var i = 0; i < 5; i++)
                    {
                        var last = route.Last();
                        var outgoingEdges = last.Edge.To.OutgoingEdges;
                        if (outgoingEdges.Count == 0) break;

                        var nextEdge = outgoingEdges.Values.ElementAt(Random.Next(0, outgoingEdges.Count));
                        route.Add(nextEdge);
                    }
                }

                break;
            }
            case 2:
            {
                // Random start and goal nodes, finds a route between them
                while (route == null)
                {
                    currentNode = environment.GetRandomNode();
                    var goal = environment.GetRandomNode();
                    if (goal == null || goal.Equals(currentNode)) continue;

                    route = environment.FindRoute(currentNode, goal);
                }

                break;
            }
            case 3:
            {
                // Finds the shortest route between start and goal nodes
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                var goal = environment.NearestNode(Position.CreateGeoPosition(destLon, destLat));

                route = environment.FindShortestRoute(currentNode, goal,
                    edge => edge.Modalities.Contains(SpatialModalityType.CarDriving)) ?? new Route();

                break;
            }
            case 4:
            {
                // Random goal selection from the nearest start node
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));

                while (route == null || route.Count == 0)
                {
                    var goal = environment.GetRandomNode();
                    route = environment.FindRoute(currentNode, goal);
                }

                break;
            }
            case 5:
            {
                // Route starts at the provided edge and continues to random nodes
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                route = new Route { startingEdge };

                while (route.Count == 1)
                {
                    var goal = environment.GetRandomNode();
                    var nextEdges = environment.FindRoute(startingEdge.To, goal, (_, edge, _) => edge.Length);

                    if (nextEdges != null)
                        foreach (var edge in nextEdges)
                            route.Add(edge.Edge);
                }

                break;
            }
            case 6:
            {
                // Creates a route based on a given OpenStreetMap (OSM) route
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                route = new Route();

                var rawRoute = osmRoute.Replace("[", "").Replace("]", "").Split(';');
                var nodeToScan = currentNode;

                foreach (var osmId in rawRoute)
                {
                    var edge = nodeToScan.OutgoingEdges.Values.Single(x => x.Attributes["osmid"].Equals(osmId));
                    route.Add(edge);
                    nodeToScan = edge.To;
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(driveMode), $"Invalid driveMode: {driveMode}");
        }

        return route;
    }
}
