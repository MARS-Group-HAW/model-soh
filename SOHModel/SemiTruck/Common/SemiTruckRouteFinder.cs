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
        double truckLength, int truckMaxIncline)
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

                    // Filter: Apply the same restrictions as Case 3
                    Func<ISpatialEdge, bool> filter = edge =>
                    {
                        const double DEFAULT_MAXHEIGHT = 4.5;
                        const double BELOW_DEFAULT_MAXHEIGHT = 3.5;

                        // Validate maxheight
                        if (edge.Attributes.TryGetValue("maxheight", out var maxHeightValue) && maxHeightValue != null)
                        {
                            var maxHeightString = maxHeightValue.ToString().Trim().ToLower();
                            if (maxHeightString == "default" && DEFAULT_MAXHEIGHT < truckHeight) return false;
                            if (maxHeightString == "below_default" && BELOW_DEFAULT_MAXHEIGHT < truckHeight)
                                return false;
                            if (maxHeightString != "none" && maxHeightString != "unsigned" &&
                                double.TryParse(maxHeightString.Split(' ')[0], out double maxHeight))
                            {
                                if (maxHeight < truckHeight) return false;
                            }
                        }

                        // Validate maxweight
                        if (edge.Attributes.TryGetValue("maxweight", out var maxWeightValue) && maxWeightValue != null)
                        {
                            var maxWeightString = maxWeightValue.ToString().Trim().ToLower();
                            if (maxWeightString != "none" && maxWeightString != "unsigned" &&
                                double.TryParse(maxWeightString.Split(' ')[0], out double maxWeight))
                            {
                                if (maxWeight < truckWeight) return false;
                            }
                        }

                        // Validate maxwidth
                        if (edge.Attributes.TryGetValue("maxwidth", out var maxWidthValue) && maxWidthValue != null)
                        {
                            var maxWidthString = maxWidthValue.ToString().Trim().ToLower();
                            if (maxWidthString != "none" &&
                                double.TryParse(maxWidthString.Split(' ')[0], out double maxWidth))
                            {
                                if (maxWidth < truckWidth) return false;
                            }
                        }

                        // Validate maxlength
                        if (edge.Attributes.TryGetValue("maxlength", out var maxLengthValue) && maxLengthValue != null)
                        {
                            var maxLengthString = maxLengthValue.ToString().Trim().ToLower();
                            if (double.TryParse(maxLengthString, out double maxLength))
                            {
                                if (maxLength < truckLength) return false;
                            }
                        }

                        // Validate incline
                        if (edge.Attributes.TryGetValue("incline", out var inclineValue) && inclineValue != null)
                        {
                            var inclineString = inclineValue.ToString().Trim().ToLower();

                            // Ignore "up", "down", "yes" (they don't provide a specific value)
                            if (inclineString == "up" || inclineString == "down" || inclineString == "yes")
                            {
                                return true; // Ignore these values
                            }

                            // Extract numeric incline value
                            if (inclineString.EndsWith("%")) // Handle "10%" or "-10%"
                            {
                                inclineString = inclineString.Replace("%", "").Trim();
                            }

                            if (double.TryParse(inclineString, out double incline))
                            {
                                if (Math.Abs(incline) > truckMaxIncline) return false;
                            }
                        }

                        return true; // Edge is valid
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

                route = environment.FindShortestRoute(currentNode, goal, edge =>
                {
                    const double DEFAULT_MAXHEIGHT = 4.5;
                    const double BELOW_DEFAULT_MAXHEIGHT = 3.5;

                    // Validate maxheight
                    if (edge.Attributes.TryGetValue("maxheight", out var maxHeightValue) && maxHeightValue != null)
                    {
                        var maxHeightString = maxHeightValue.ToString().Trim().ToLower();
                        if (maxHeightString == "default")
                        {
                            if (DEFAULT_MAXHEIGHT < truckHeight) return false;
                        }
                        else if (maxHeightString == "below_default")
                        {
                            if (BELOW_DEFAULT_MAXHEIGHT < truckHeight) return false;
                        }
                        else if (maxHeightString == "none" || maxHeightString == "unsigned")
                        {
                            return true; // Unrestricted
                        }
                        else if (double.TryParse(maxHeightString.Split(' ')[0], out double maxHeight))
                        {
                            if (maxHeight < truckHeight) return false;
                        }
                    }

                    // Validate maxweight
                    if (edge.Attributes.TryGetValue("maxweight", out var maxWeightValue) && maxWeightValue != null)
                    {
                        var maxWeightString = maxWeightValue.ToString().Trim().ToLower();
                        if (maxWeightString != "none" && maxWeightString != "unsigned" &&
                            double.TryParse(maxWeightString.Split(' ')[0], out double maxWeight))
                        {
                            if (maxWeight < truckWeight) return false; // Exclude if maxweight < truck weight
                        }
                    }

                    // Validate maxwidth
                    if (edge.Attributes.TryGetValue("maxwidth", out var maxWidthValue) && maxWidthValue != null)
                    {
                        var maxWidthString = maxWidthValue.ToString().Trim().ToLower();
                        if (maxWidthString != "none" &&
                            double.TryParse(maxWidthString.Split(' ')[0], out double maxWidth))
                        {
                            if (maxWidth < truckWidth) return false;
                        }
                    }

                    // Validate maxlength
                    if (edge.Attributes.TryGetValue("maxlength", out var maxLengthValue) && maxLengthValue != null)
                    {
                        var maxLengthString = maxLengthValue.ToString().Trim().ToLower();
                        if (double.TryParse(maxLengthString, out double maxLength))
                        {
                            if (maxLength < truckLength) return false; // Exclude if maxlength < truck length
                        }
                    }

                    // Validate incline
                    if (edge.Attributes.TryGetValue("incline", out var inclineValue) && inclineValue != null)
                    {
                        var inclineString = inclineValue.ToString().Trim().ToLower();

                        // Ignore "up", "down", "yes" (no value)
                        if (inclineString == "up" || inclineString == "down" || inclineString == "yes")
                        {
                            return true; // Ignore these values
                        }

                        // Extract numeric incline value
                        if (inclineString.EndsWith("%")) // Handle "10%" or "-10%"
                        {
                            inclineString = inclineString.Replace("%", "").Trim();
                        }

                        if (double.TryParse(inclineString, out double incline))
                        {
                            if (Math.Abs(incline) > truckMaxIncline)
                                return false; // Exclude if incline exceeds max limit
                        }
                    }

                    return true; // Include edges that pass all constraints
                }) ?? new Route();

                // Validate the resulting route
                if (route == null || route.Count == 0)
                {
                    Console.WriteLine("No valid route found. Constraints may be too strict or data incomplete.");
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
}