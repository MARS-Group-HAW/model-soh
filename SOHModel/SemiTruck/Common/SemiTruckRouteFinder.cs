using Mars.Common.Core;
using Mars.Common.Core.Collections;
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

    private static readonly SemiTruckRouteCacheManager routeCache;

    static SemiTruckRouteFinder()
    {
        routeCache = new SemiTruckRouteCacheManager("route_cache.db");
    }

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
                // Identify start and goal nodes based on geographic coordinates
                currentNode = environment.NearestNode(Position.CreateGeoPosition(startLon, startLat), outgoingModality: SpatialModalityType.CarDriving);
                var goal = environment.NearestNode(Position.CreateGeoPosition(destLon, destLat), incomingModality: SpatialModalityType.CarDriving);
                // Retrieve the start and goal edge IDs to be used as keys for route caching
                string? startEdgeId = currentNode.OutgoingEdges.Values.FirstOrDefault()?.GetId()?.ToString();
                string? goalEdgeId = goal.IncomingEdges.Values.FirstOrDefault()?.GetId()?.ToString();

                List<string> cachedEdgeIds;
                bool wasSuboptimal;

                //Try to use a cached route if available and valid
                if (startEdgeId != null && goalEdgeId != null)
                {
                    bool cacheHit = routeCache.TryGetRoute(
                        startEdgeId,
                        goalEdgeId,
                        truckWeight,
                        truckHeight,
                        truckWidth,
                        truckLength,
                        truckMaxIncline,
                        out cachedEdgeIds,
                        out wasSuboptimal,
                        out bool exactConstraintMatch
                    );

                    if (cacheHit)
                    {
                        // Convert cached edge IDs back into a usable route object
                        var candidateRoute = ConvertEdgeIdsToRoute(cachedEdgeIds, environment, RemovedEdges);

                        bool hasRemovedEdge = false;

                        if (candidateRoute == null)
                        {
                            hasRemovedEdge = true;
                            Console.WriteLine("Cached route is invalid – missing edges in route.");
                        }


                        // If the cached route is valid and either exact or acceptable, use it
                        if ((!wasSuboptimal || exactConstraintMatch) && !hasRemovedEdge)
                        {
                            // Console.WriteLine("Route from cache used.");
                            route = candidateRoute;
                            break;
                        }

                        if (hasRemovedEdge)
                        {
                            Console.WriteLine("Cached route contains removed edges – skipping cache.");
                        }
                    }
                }

                // Compute a new route considering all physical and logical constraints
                Route validRoute = new Route(); // Collects the longest valid sub-route if a complete route fails
                bool routeWasLimitedByConstraints = false;
                bool isPartial = false;

                // Find a constraint-aware shortest route, edge by edge
                route = environment.FindShortestRoute(currentNode, goal, edge =>
                {
                    if (RemovedEdges.Contains(edge))
                    {
                        return false; // This edge is currently closed
                    }

                    bool isValid = CheckValidEdge(edge, truckHeight, truckWeight, truckWidth, truckLength,
                        truckMaxIncline);
                    if (isValid)
                    {
                        List<ISpatialLane> lanes = edge.Lanes?.ToList();
                        var desiredLane = lanes?.FirstOrDefault();
                        int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                        validRoute.Add(edge, desiredLaneIndex); // Track as part of the partial route
                    }
                    else
                    {
                        routeWasLimitedByConstraints = true;
                    }

                    return isValid;
                    //return true;
                }) ?? new Route();


                // If a full route could not be computed, try to extract a partial route to the last valid point
                if ((route == null || route.Count == 0) && validRoute.Count > 0)
                {
                    //Console.WriteLine("No complete route found, but a partial route is available.");
                    //Console.WriteLine($"Last reachable position: {validRoute.Goal}");
                    var validGoal = validRoute.Last().Edge.To;

                    //Attempt to compute a shorter route from the start to the last reachable node
                    route = environment.FindShortestRoute(currentNode, validGoal, edge =>
                    {
                        bool isValid = CheckValidEdge(edge, truckHeight, truckWeight, truckWidth, truckLength,
                            truckMaxIncline);
                        return isValid;
                    }) ?? new Route();
                }
                else if (validRoute.Count == 0)
                {
                    // No route found that satisfies constraints; fallback to empty result
                    Console.WriteLine("No valid route found. Constraints may be too strict or data incomplete.");
                    route = new Route(); // Return an empty route
                }

                // Store the new valid route in the cache (only if it's usable and based on constraint filtering)
                if (route.Count > 0 && !route.Any(e => RemovedEdges.Contains(e.Edge)) && startEdgeId != null &&
                    goalEdgeId != null)
                {
                    List<string> edgeIds = route.Select(e => e.Edge.GetId().ToString()).ToList();


                    routeCache.StoreRoute(
                        startEdgeId,
                        goalEdgeId,
                        edgeIds,
                        truckWeight,
                        truckHeight,
                        truckWidth,
                        truckLength,
                        truckMaxIncline,
                        wasSuboptimal: routeWasLimitedByConstraints
                    );
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

    /// <summary>
    /// Attempts to reconstruct a Route object from a list of edge ID strings.
    /// The method checks whether any of the referenced edges are currently removed (e.g., due to road closures).
    /// If any edge is no longer part of the environment, or is marked as removed, the method returns null.
    /// </summary>
    /// <param name="edgeIds">List of edge IDs (as strings) representing the route path.</param>
    /// <param name="environment">The spatial graph environment containing current valid edges.</param>
    /// <param name="RemovedEdges">List of currently removed or blocked edges.</param>
    /// <returns>A rebuilt Route object if all edges are valid and available; otherwise null.</returns>
    private static Route? ConvertEdgeIdsToRoute(List<string> edgeIds, ISpatialGraphEnvironment environment,
        List<ISpatialEdge> RemovedEdges)
    {
        // Reject route if it includes any edge currently marked as removed
        if (edgeIds.Any(id => RemovedEdges.Any(e => e.GetId().ToString() == id)))
        {
            Console.WriteLine("Cached route contains a removed edge – discarding.");
            return null;
        }

        // Attempt to resolve all edge IDs into actual edge objects from the environment
        var rebuiltEdges = edgeIds
            .Select(idStr =>
            {
                if (int.TryParse(idStr, out var id) && environment.Edges.TryGetValue(id, out var edge))
                    return edge;
                return null;
            })
            .Where(edge => edge != null)
            .ToList();

        // Rebuild route from resolved edges
        var rebuiltRoute = new Route();
        foreach (var edge in rebuiltEdges)
        {
            List<ISpatialLane> lanes = edge.Lanes?.ToList();
            var desiredLane = lanes?.FirstOrDefault();
            int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;

            rebuiltRoute.Add(edge, desiredLaneIndex);
        }

        return rebuiltRoute;
    }
}