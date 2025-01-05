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

                    var routeComplete = true;
                    for (var i = 0; i < 5; i++)
                    {
                        var last = route.Last();
                        var edgeCount = last.Edge.To.OutgoingEdges.Count;
                        if (edgeCount == 0)
                        {
                            //_logger.LogWarning("Dead end found");
                            routeComplete = false;
                            break;
                        }

                        var randomLane = Random.Next(0, edgeCount);
                        var nextEdge = last.Edge.To.OutgoingEdges.Values.ElementAt(randomLane);
                        route.Add(nextEdge);
                    }

                    if (routeComplete)
                        break;
                }

                break;
            }
            case 2:
            {
                // Random start and goal nodes, finds a route between them
                while (route == null)
                {
                    //TODO Random Node can be out of range
                    currentNode = environment.GetRandomNode();
                    if (currentNode == null) continue;

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

                ISpatialNode goal = null;
                while (route == null || goal.Equals(currentNode) || route.Count == 0)
                {
                    goal = environment.GetRandomNode();
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

                    if (!goal.Equals(currentNode) || nextEdges != null)
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
                    var res = nodeToScan.OutgoingEdges.Values.Single(x => x.Attributes["osmid"].Equals(osmId));

                    route.Add(res);
                    nodeToScan = res.To;
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(driveMode));
        }

        return route;
    }
}
