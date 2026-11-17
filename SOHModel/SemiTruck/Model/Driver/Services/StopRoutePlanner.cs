using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.Services;

/// <summary>
/// Plans routes via a stop facility (rest or refuel). A plan is only produced if the facility is reachable within
/// the energy and legal driving time left, and a complete route continues from it to the destination.
/// </summary>
/// <remarks>
/// A plan consists of three parts:
/// <list type="number">
/// <item>the current route up to the approach node, then the fastest route from there to the facility node;</item>
/// <item>the fastest connector from the facility back to the route, rejoining within
/// <see cref="SemiTruckDriverConstants.ForwardRejoinLookaheadMeters"/> ahead (or at the destination);</item>
/// <item>the unchanged rest of the current route.</item>
/// </list>
/// A facility beside the route without an own node in the network is entered from the route itself, without detour.
/// </remarks>
public sealed class StopRoutePlanner
{
    private readonly SemiTruckLayer _layer;
    private readonly ISpatialGraphEnvironment _environment;
    private readonly SemiTruck _truck;

    public StopRoutePlanner(SemiTruckLayer layer, ISpatialGraphEnvironment environment, SemiTruck truck)
    {
        _layer = layer;
        _environment = environment;
        _truck = truck;
    }

    /// <summary>
    /// Search for one planning attempt of <paramref name="state"/>: routes found while comparing candidates are
    /// shared, rejections are counted for the failure details, and the applied plan marks the stop as planned.
    /// </summary>
    public StopRouteSearch CreateSearch(SemiTruckSteeringHandle steering, SemiTruckDriver driver, StopState state)
    {
        var connectors = new Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>();
        var rejections = new Dictionary<StopRouteRejection, int>();
        return new StopRouteSearch(
            (lat, lon, approach) => Create(lat, lon, approach, steering, driver, connectors,
                reason => rejections[reason] = rejections.GetValueOrDefault(reason) + 1),
            plan =>
            {
                Apply(plan, steering, driver);
                state.MarkPlanned();
                state.SetTargetNode(plan.StopNode);
                driver.FuelTracker.MarkRouteChanged();
            },
            () => string.Join(", ", rejections.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
    }

    /// <summary>
    /// Plans the route via the facility at (<paramref name="lat"/>, <paramref name="lon"/>), leaving the current
    /// route at <paramref name="approach"/>; <c>null</c> if there is no safe, complete route.
    /// </summary>
    public StopRoutePlan? Create(double lat, double lon, ISpatialNode approach,
        SemiTruckSteeringHandle steering, SemiTruckDriver driver,
        Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors = null,
        Action<StopRouteRejection>? reportRejection = null)
    {
        StopRoutePlan? Reject(StopRouteRejection reason)
        {
            reportRejection?.Invoke(reason);
            return null;
        }

        var remaining = steering.RemainingSteps.ToList();
        int approachIndex = remaining.FindIndex(step => step.Edge.From == approach);
        if (approachIndex < 1) return Reject(StopRouteRejection.InvalidApproach);

        var stopNodes = SnapFacility(lat, lon, approach, remaining);
        // A facility without an own node nearby is not modelled by the network; enter it from the route.
        bool modelled = stopNodes.Any(node => Distance(lat, lon, node) <= SemiTruckDriverConstants.FacilityNodeMatchMeters);
        if (!modelled && CreateRoadsideStop(lat, lon, remaining, approachIndex, driver) is { } roadside)
            return roadside;
        if (stopNodes.Count == 0) return Reject(StopRouteRejection.UnusableFacility);

        StopRouteRejection? lastRejection = null;
        foreach (var stopNode in stopNodes)
        {
            var plan = CreateVia(stopNode, remaining, approachIndex, driver, connectors, out var rejection);
            if (plan != null) return plan;
            lastRejection = rejection;
        }
        return Reject(lastRejection ?? StopRouteRejection.UnusableFacility);
    }

    /// <summary>Makes <paramref name="plan"/> the truck's route.</summary>
    public void Apply(StopRoutePlan plan, SemiTruckSteeringHandle steering, SemiTruckDriver driver) =>
        _layer.ReplaceRoute(driver, steering, plan.Route);

    /// <summary>
    /// Nodes to enter the facility at: the nearest drivable nodes within the detour tolerance and those almost as
    /// near, as the nearest one may lie on the opposite carriageway or a disconnected stub. Nodes of the route itself
    /// are excluded, as stopping there would mean stopping on the carriageway (see <see cref="CreateRoadsideStop"/>).
    /// </summary>
    private List<ISpatialNode> SnapFacility(double lat, double lon, ISpatialNode approach, List<EdgeStop> remaining)
    {
        if (Distance(lat, lon, approach) < 1) return new List<ISpatialNode> { approach };

        var routeNodes = remaining.Select(step => step.Edge.To).ToHashSet();
        var near = _environment.NearestNodes(Position.CreateGeoPosition(lon, lat), double.MaxValue,
                SemiTruckDriverConstants.FacilitySnapCandidates)
            .Where(node => !routeNodes.Contains(node) && IsDrivable(node))
            .Select(node => (Node: node, Meters: Distance(lat, lon, node)))
            .Where(candidate => candidate.Meters <= SemiTruckDriverConstants.MaxFacilityDetourMeters)
            .OrderBy(candidate => candidate.Meters)
            .ToList();
        if (near.Count == 0) return new List<ISpatialNode>();
        double limit = near[0].Meters + SemiTruckDriverConstants.FacilitySnapToleranceMeters;
        return near.Where(candidate => candidate.Meters <= limit).Select(candidate => candidate.Node).ToList();
    }

    /// <summary>
    /// The network often models a service area only as the motorway passing it. A facility right beside a
    /// route edge is then entered from that edge, stopping at the nearer end node, instead of being snapped to
    /// a node of another road (which typically means a long detour or a U-turn on the opposite carriageway).
    /// </summary>
    private StopRoutePlan? CreateRoadsideStop(double lat, double lon, List<EdgeStop> remaining, int approachIndex,
        SemiTruckDriver driver)
    {
        for (int j = approachIndex; j < remaining.Count - 1; j++)
        {
            var edge = remaining[j].Edge;
            var geometry = edge.Geometry is { Length: > 1 } g ? g : new[] { edge.From.Position, edge.To.Position };
            var (meters, fraction) = GeometryHelper.DistanceToPolyline(lat, lon, geometry);
            if (meters > SemiTruckDriverConstants.RoadsideFacilityMeters) continue;

            int stopIndex = fraction < 0.5 ? j : j + 1;
            var approach = remaining.Take(stopIndex).Select(step => step.Edge).ToList();
            if (!CanReach(approach, driver)) return null;
            var onward = remaining.Skip(stopIndex).Select(step => step.Edge).ToList();
            return Plan(BuildRoute(remaining), remaining[stopIndex].Edge.From, approach,
                RouteEstimate.Of(onward, 0, _truck));
        }
        return null;
    }

    private StopRoutePlan? CreateVia(ISpatialNode stopNode, List<EdgeStop> remaining, int approachIndex,
        SemiTruckDriver driver, Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors,
        out StopRouteRejection? rejection)
    {
        rejection = null;
        var toStop = SemiTruckRouteFinder.FindFastestComplete(_environment, remaining[approachIndex].Edge.From,
            stopNode, _truck, _layer.RemovedEdges);
        if (toStop == null)
        {
            rejection = StopRouteRejection.NoApproachRoute;
            return null;
        }

        var approach = remaining.Take(approachIndex).Concat(toStop).Select(step => step.Edge).ToList();
        if (!approach.All(IsUsable))
        {
            rejection = StopRouteRejection.InvalidApproach;
            return null;
        }
        if (!CanReach(approach, driver))
        {
            rejection = StopRouteRejection.EnergyOrDrivingLimit;
            return null;
        }

        if (FindOnward(stopNode, remaining, approachIndex, connectors) is not var (onward, estimate))
        {
            rejection = StopRouteRejection.NoRejoinRoute;
            return null;
        }
        return Plan(BuildRoute(remaining.Take(approachIndex), toStop, onward), stopNode, approach, estimate);
    }

    /// <summary>
    /// The fastest way from the facility to the destination: a connector to a node of the remaining route up to
    /// the rejoin lookahead (or the destination itself), followed by the unchanged route from there.
    /// </summary>
    private (List<EdgeStop> Route, RouteEstimate Estimate)? FindOnward(ISpatialNode stopNode,
        List<EdgeStop> remaining, int approachIndex,
        Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors)
    {
        (List<EdgeStop> Route, RouteEstimate Estimate)? best = null;
        double along = 0;
        for (int rejoinIndex = approachIndex; rejoinIndex <= remaining.Count; rejoinIndex++)
        {
            if (along > SemiTruckDriverConstants.ForwardRejoinLookaheadMeters) break;
            if (TryRejoin(stopNode, remaining, rejoinIndex, connectors) is var (onward, estimate) &&
                (best is not { } current || estimate.Seconds < current.Estimate.Seconds - 1e-6 ||
                 (Math.Abs(estimate.Seconds - current.Estimate.Seconds) <= 1e-6 &&
                  estimate.Meters < current.Estimate.Meters)))
                best = (onward, estimate);
            if (rejoinIndex < remaining.Count) along += remaining[rejoinIndex].Edge.Length;
        }
        return best;
    }

    /// <summary>Connector to the route node at <paramref name="rejoinIndex"/>, followed by the route from there.</summary>
    private (List<EdgeStop> Route, RouteEstimate Estimate)? TryRejoin(ISpatialNode stopNode,
        List<EdgeStop> remaining, int rejoinIndex,
        Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors)
    {
        var suffix = remaining.Skip(rejoinIndex).ToList();
        if (!suffix.All(step => IsUsable(step.Edge))) return null;
        var rejoinNode = rejoinIndex < remaining.Count ? remaining[rejoinIndex].Edge.From : remaining[^1].Edge.To;
        if (FindConnector(stopNode, rejoinNode, connectors) is not { } connector) return null;

        var onward = connector.Concat(suffix).ToList();
        return (onward, RouteEstimate.Of(onward.Select(step => step.Edge), 0, _truck));
    }

    private Route? FindConnector(ISpatialNode from, ISpatialNode to,
        Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors)
    {
        if (connectors != null && connectors.TryGetValue((from, to), out var cached)) return cached;
        var connector = SemiTruckRouteFinder.FindFastestComplete(_environment, from, to, _truck, _layer.RemovedEdges);
        if (connectors != null) connectors[(from, to)] = connector;
        return connector;
    }

    private StopRoutePlan Plan(Route route, ISpatialNode stopNode, List<ISpatialEdge> approach,
        RouteEstimate onward)
    {
        RouteDistanceHelper.Discount(route, _truck.PositionOnCurrentEdge);
        return new StopRoutePlan(route, stopNode, RouteEstimate.Of(approach, _truck.PositionOnCurrentEdge, _truck),
            onward);
    }

    private bool CanReach(List<ISpatialEdge> approach, SemiTruckDriver driver) =>
        StopReachability.CanReach(approach, _truck.PositionOnCurrentEdge, _truck, driver.FuelTracker,
            driver.DrivingTime, _layer._tickDuration);

    private bool IsUsable(ISpatialEdge edge) => SemiTruckRouteFinder.IsUsableEdge(edge, _truck, _layer.RemovedEdges);

    private static bool IsDrivable(ISpatialNode node) =>
        node.IncomingEdges.Values.Any(edge => edge.Modalities.Contains(SpatialModalityType.CarDriving)) &&
        node.OutgoingEdges.Values.Any(edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));

    private static double Distance(double lat, double lon, ISpatialNode node) =>
        GeometryHelper.ApproxDistanceMeters(lat, lon, node.Position.Latitude, node.Position.Longitude);

    private static Route BuildRoute(params IEnumerable<EdgeStop>[] parts)
    {
        var route = new Route();
        foreach (var part in parts)
            foreach (var step in part) route.Add(step.Edge, step.DesiredLane);
        return route;
    }
}

/// <summary>A route via a stop facility, with estimates for the approach and the way on to the destination.</summary>
public sealed record StopRoutePlan(Route Route, ISpatialNode StopNode, RouteEstimate Approach, RouteEstimate Onward);

/// <summary>
/// Route planning used while choosing a stop: <see cref="Create"/> evaluates a candidate without changing the
/// truck's route, <see cref="Apply"/> commits the chosen plan. Created by <see cref="StopRoutePlanner.CreateSearch"/>.
/// </summary>
public sealed record StopRouteSearch(
    Func<double, double, ISpatialNode, StopRoutePlan?> Create,
    Action<StopRoutePlan> Apply,
    Func<string>? ExplainFailure = null);

public enum StopRouteRejection
{
    InvalidApproach, UnusableFacility, NoApproachRoute, EnergyOrDrivingLimit, NoRejoinRoute
}
