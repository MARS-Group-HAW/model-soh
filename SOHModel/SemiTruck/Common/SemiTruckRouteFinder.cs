using Mars.Common.Core;
using Mars.Components.Environments;
using NetTopologySuite.Planargraph;
using ServiceStack;

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
        ISpatialEdge startingEdge, string osmRoute, double truckHeight, double truckWeight, double truckWidth,
        double truckLength, int truckMaxIncline, List<ISpatialEdge> RemovedEdges)
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
                    // Get a random starting node
                    currentNode = environment.GetRandomNode();
                    if (currentNode == null) continue;

                    // Get a random goal node
                    var goal = environment.GetRandomNode();
                    if (goal == null || goal.Equals(currentNode)) continue;

                    // Heuristic: Minimize edge length (shortest route)
                    Func<ISpatialNode, ISpatialEdge, ISpatialNode, double> heuristic = (from, edge, to) => edge.Length;

                    // Filter: Use CheckValidEdge method
                    Func<ISpatialEdge, bool> filter = edge =>
                    {
                        bool isValid = CheckValidEdge(edge, truckHeight, truckWeight, truckWidth, truckLength,
                            truckMaxIncline);
                        if (!isValid)
                        {
                            Console.WriteLine($"Route blocked: Edge {edge.GetId()} does not meet constraints.");
                        }

                        return isValid;
                    };

                    // Apply the heuristic and filter when finding a route
                    route = environment.FindRoute(currentNode, goal, heuristic, filter);
                }

                break;
            }


            case 3:
            {
                // Finds the shortest route between start and goal nodes
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
                var goal = environment.NearestNode(Position.CreateGeoPosition(destLon, destLat));
                
                Route validRoute = new Route(); // Stores the longest reachable route

                route = environment.FindShortestRoute(currentNode, goal, edge =>
                {
                    if (RemovedEdges.Contains(edge))
                    {
                        return false;
                    }
                    bool isValid = CheckValidEdge(edge, truckHeight, truckWeight, truckWidth, truckLength,
                        truckMaxIncline);
                    if (isValid)
                    {
                        validRoute.Add(edge);
                    }

                    return isValid;
                    //return true;
                }) ?? new Route();


                // Compute Partial Route if possible
                if ((route == null || route.Count == 0) && validRoute.Count > 0)
                {
                    Console.WriteLine("No complete route found, but a partial route is available.");
                    //Console.WriteLine($"Last reachable position: {validRoute.Goal}");
                    var validGoal = validRoute.Last().Edge.To;
                    route = environment.FindShortestRoute(currentNode, validGoal, edge =>
                    {
                        bool isValid = CheckValidEdge(edge, truckHeight, truckWeight, truckWidth, truckLength,
                            truckMaxIncline);
                        return isValid;
                    }) ?? new Route();
                }
                else if (validRoute.Count == 0)
                {
                    Console.WriteLine("No valid route found. Constraints may be too strict or data incomplete.");
                    route = new Route(); // Return an empty route
                }

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

    /// <summary>
    ///     Checks if an edge is valid based on truck constraints.
    /// </summary>
    private static bool CheckValidEdge(ISpatialEdge edge, double truckHeight, double truckWeight, double truckWidth,
        double truckLength, int truckMaxIncline)
    {
        const double DEFAULT_MAXHEIGHT = 4.5;
        const double BELOW_DEFAULT_MAXHEIGHT = 3.5;

        static string GetFirstNumber(string input)
        {
            int spaceIndex = input.IndexOf(' ');
            return spaceIndex >= 0 ? input.Substring(0, spaceIndex) : input;
        }

        // Validate maxheight
        if (edge.Attributes.TryGetValue("maxheight", out var maxHeightValue) && maxHeightValue != null)
        {
            var maxHeightString = maxHeightValue.ToString().Trim();
            if (maxHeightString.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                DEFAULT_MAXHEIGHT < truckHeight) return false;
            if (maxHeightString.Equals("below_default", StringComparison.OrdinalIgnoreCase) &&
                BELOW_DEFAULT_MAXHEIGHT < truckHeight) return false;
            if (!maxHeightString.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !maxHeightString.Equals("unsigned", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(GetFirstNumber(maxHeightString), out double maxHeight))
            {
                if (maxHeight < truckHeight) return false;
            }
        }

        // Validate maxweight
        if (edge.Attributes.TryGetValue("maxweight", out var maxWeightValue) && maxWeightValue != null)
        {
            var maxWeightString = maxWeightValue.ToString().Trim();
            if (!maxWeightString.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !maxWeightString.Equals("unsigned", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(GetFirstNumber(maxWeightString), out double maxWeight))
            {
                if (maxWeight < truckWeight) return false;
            }
        }

        // Validate maxwidth
        if (edge.Attributes.TryGetValue("maxwidth", out var maxWidthValue) && maxWidthValue != null)
        {
            var maxWidthString = maxWidthValue.ToString().Trim();
            if (!maxWidthString.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(GetFirstNumber(maxWidthString), out double maxWidth))
            {
                if (maxWidth < truckWidth) return false;
            }
        }

        // Validate maxlength
        if (edge.Attributes.TryGetValue("maxlength", out var maxLengthValue) && maxLengthValue != null)
        {
            var maxLengthString = maxLengthValue.ToString().Trim();
            if (double.TryParse(maxLengthString, out double maxLength))
            {
                if (maxLength < truckLength) return false;
            }
        }

        double maxIncline = 0.0;
        // Validate incline
        if (edge.Attributes.TryGetValue("incline", out var inclineValue) && inclineValue != null)
        {
            var inclineString = inclineValue.ToString().Trim();
            if (!inclineString.Equals("up", StringComparison.OrdinalIgnoreCase) &&
                !inclineString.Equals("down", StringComparison.OrdinalIgnoreCase) &&
                !inclineString.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                if (inclineString.EndsWith("%")) inclineString = inclineString[..^1].Trim();

                if (double.TryParse(inclineString, out double incline))
                {
                    maxIncline = Math.Abs(incline);
                }
            }
        }

        return maxIncline <= truckMaxIncline;
    }
}