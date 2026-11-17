using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.Services
{
    /// <summary>
    /// Manages routing, rerouting, bypass creation, and stop planning for a SemiTruckDriver.
    /// </summary>
    public class RouteManager
    {
        private readonly SemiTruckLayer _layer;
        private readonly ISpatialGraphEnvironment _environment;
        private readonly SemiTruck _truck;
        private readonly int _driveMode;
        private readonly UnregisterAgent _unregister;

        public RouteManager(SemiTruckLayer layer, ISpatialGraphEnvironment environment, SemiTruck truck,
            int driveMode, UnregisterAgent unregister)
        {
            _layer = layer;
            _environment = environment;
            _truck = truck;
            _driveMode = driveMode;
            _unregister = unregister;
        }

        /// <summary>
        /// Looks ahead in the route and creates bypass if blocked edge found.
        /// </summary>
        /// <returns>True if route is valid or successfully updated</returns>
        public bool LookaheadAndBypass(SemiTruckSteeringHandle steeringHandle, SemiTruckDriver driver)
        {
            if (steeringHandle.Route.Count == 0)
                return true;

            double distanceAhead = 0;

            for (int i = 0; i < steeringHandle.Route.Count; i++)
            {
                var edge = steeringHandle.Route[i].Edge;
                distanceAhead += edge.Length;

                if (_layer.RemovedEdges.Contains(edge))
                {
                    return CreateBypass(edge, steeringHandle, driver);
                }

                if (distanceAhead >= SemiTruckDriverConstants.LookaheadDistance)
                    break;
            }

            return true;
        }

        /// <summary>
        /// Creates a bypass route around a removed/blocked edge.
        /// </summary>
        public bool CreateBypass(ISpatialEdge removedEdge, SemiTruckSteeringHandle steeringHandle, SemiTruckDriver driver)
        {
            if (_layer.notifyTrucks) _layer.UnregisterTruckFromRoute(driver, steeringHandle.Route);

            var stops = steeringHandle.Route.Stops;
            int passed = steeringHandle.Route.PassedStops;
            if (stops == null || stops.Count == 0 || passed >= stops.Count)
            {
                Console.WriteLine("Route has no remaining stops. Stopping truck.");
                _unregister.Invoke(_layer, driver);
                return false;
            }

            // Find first valid EdgeStop AFTER the removed section
            var candidateStops = steeringHandle.Route.Stops
                .Skip(steeringHandle.Route.PassedStops)
                .SkipWhile(stop => stop.Edge != removedEdge)
                .SkipWhile(stop => _layer.RemovedEdges.Contains(stop.Edge))
                .Where(stop => !_layer.RemovedEdges.Contains(stop.Edge));

            Route bypassRoute = null;
            EdgeStop? nextValidEdgeStop = null;

            foreach (var candidate in candidateStops)
            {
                bypassRoute = SemiTruckRouteFinder.Find(
                    _environment, _driveMode, steeringHandle.Position.Latitude,
                    steeringHandle.Position.Longitude,
                    candidate.Edge.From.Position.Latitude, candidate.Edge.From.Position.Longitude,
                    null, "", _truck.Height, _truck.Mass,
                    _truck.Width, _truck.Length, _truck.MaxIncline, _layer.RemovedEdges, true
                );

                if (bypassRoute != null && bypassRoute.Count > 0)
                {
                    nextValidEdgeStop = candidate;
                    break;
                }
            }

            if (nextValidEdgeStop == null || bypassRoute == null || bypassRoute.Count == 0)
            {
                Console.WriteLine($"No alternative bypass route found up to destination. Stopping truck.");
                _unregister.Invoke(_layer, driver);
                return false;
            }

            // While the current EdgeStop is invalid, keep removing
            int removeIndex = steeringHandle.Route.PassedStops;
            while (steeringHandle.Route.Stops.Count > 0 &&
                   !steeringHandle.Route.Stops[removeIndex].Equals(nextValidEdgeStop))
            {
                steeringHandle.Route.Stops.RemoveAt(removeIndex);
            }

            // Check if byPass already leads to goal
            var originalLastStop = steeringHandle.Route.Stops.LastOrDefault();
            bool bypassEndsAtGoal =
                originalLastStop != null &&
                bypassRoute.LastOrDefault()?.Edge?.To?.Equals(originalLastStop.Edge.To) == true;

            if (!bypassEndsAtGoal)
            {
                // Append the remaining part of the original route (after bypass) to the new route
                foreach (var edgeStop in steeringHandle.Route)
                {
                    List<ISpatialLane> lanes = edgeStop.Edge.Lanes?.ToList();
                    var desiredLane = lanes?.FirstOrDefault();
                    int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                    bypassRoute.Add(edgeStop.Edge, desiredLaneIndex);
                }
            }

            // Assign the final composed route to the truck
            steeringHandle.Route = bypassRoute;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(driver, steeringHandle.Route);

            return true;
        }

        /// <summary>
        /// Plans a route with a temporary stop (rest or refuel).
        /// </summary>
        public void PlanRouteWithStop(double targetLat, double targetLon, ISpatialNode insertFromNode,
            SemiTruckSteeringHandle steeringHandle, SemiTruckDriver driver, StopType stopType,
            Action<ISpatialNode> onSuccess, Action onFailure)
        {
            var plan = CreateStopRoute(targetLat, targetLon, insertFromNode, steeringHandle, driver);
            if (plan == null) onFailure();
            else ApplyStopRoute(plan, steeringHandle, driver, onSuccess);
        }

        public StopRoutePlan? CreateStopRoute(double targetLat, double targetLon, ISpatialNode insertFromNode,
            SemiTruckSteeringHandle steeringHandle, SemiTruckDriver driver,
            Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors = null,
            Action<StopRouteRejection>? reportRejection = null)
        {
            StopRoutePlan? Reject(StopRouteRejection reason)
            {
                reportRejection?.Invoke(reason);
                return null;
            }

            var remaining = steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops).ToList();
            int insertIndex = remaining.FindIndex(stop => stop.Edge.From == insertFromNode);
            if (insertIndex < 1) return Reject(StopRouteRejection.InvalidApproach);

            var facilityPosition = Position.CreateGeoPosition(targetLon, targetLat);
            // Snapping to a node of the route itself would be a stop on the carriageway; a facility right beside the
            // route is entered from it instead (CreateRoadsideStop).
            var routeNodes = remaining.Select(stop => stop.Edge.To).ToHashSet();
            var stopNodes = GeometryHelper.ApproxDistanceMeters(targetLat, targetLon,
                insertFromNode.Position.Latitude, insertFromNode.Position.Longitude) < 1
                ? new List<ISpatialNode> { insertFromNode }
                : _environment.NearestNodes(facilityPosition, double.MaxValue, SemiTruckDriverConstants.FacilitySnapCandidates)
                    .Where(node => !routeNodes.Contains(node) && node.IncomingEdges.Values.Any(e => e.Modalities.Contains(SpatialModalityType.CarDriving)) &&
                                   node.OutgoingEdges.Values.Any(e => e.Modalities.Contains(SpatialModalityType.CarDriving)))
                    .Select(node => (Node: node, Meters: GeometryHelper.ApproxDistanceMeters(targetLat, targetLon,
                        node.Position.Latitude, node.Position.Longitude)))
                    .Where(c => c.Meters <= SemiTruckDriverConstants.MaxFacilityDetourMeters)
                    .OrderBy(c => c.Meters).ToList() is var near && near.Count > 0
                    ? near.Where(c => c.Meters <= near[0].Meters + SemiTruckDriverConstants.FacilitySnapToleranceMeters)
                        .Select(c => c.Node).ToList()
                    : new List<ISpatialNode>();
            // A facility without an own node nearby is not modelled by the network; enter it from the route.
            bool modelled = stopNodes.Count > 0 && stopNodes.Any(node => GeometryHelper.ApproxDistanceMeters(
                targetLat, targetLon, node.Position.Latitude, node.Position.Longitude) <=
                SemiTruckDriverConstants.FacilityNodeMatchMeters);
            if (!modelled)
            {
                var roadside = CreateRoadsideStop(targetLat, targetLon, remaining, insertIndex, driver);
                if (roadside != null) return roadside;
            }
            if (stopNodes.Count == 0) return Reject(StopRouteRejection.UnusableFacility);

            // The nearest node may lie on the opposite carriageway or a disconnected stub; try nearby alternatives.
            StopRouteRejection? lastRejection = null;
            foreach (var stopNode in stopNodes)
            {
                var plan = CreateStopRouteVia(stopNode, remaining, insertIndex, insertFromNode, driver, connectors,
                    out var rejection);
                if (plan != null) return plan;
                lastRejection = rejection;
            }
            return Reject(lastRejection ?? StopRouteRejection.UnusableFacility);
        }

        /// <summary>
        /// The network often models a service area only as the motorway passing it. A facility right beside a
        /// route edge is then entered from that edge, stopping at the nearer end node, instead of being snapped to
        /// a node of another road (which typically means a long detour or a U-turn on the opposite carriageway).
        /// </summary>
        private StopRoutePlan? CreateRoadsideStop(double targetLat, double targetLon, List<EdgeStop> remaining,
            int insertIndex, SemiTruckDriver driver)
        {
            for (int j = insertIndex; j < remaining.Count - 1; j++)
            {
                var edge = remaining[j].Edge;
                var geometry = edge.Geometry is { Length: > 1 } g ? g : new[] { edge.From.Position, edge.To.Position };
                var (meters, fraction) = GeometryHelper.DistanceToPolyline(targetLat, targetLon, geometry);
                if (meters > SemiTruckDriverConstants.RoadsideFacilityMeters) continue;

                int stopIndex = fraction < 0.5 ? j : j + 1;
                var approach = remaining.Take(stopIndex).Select(step => step.Edge).ToList();
                var onward = remaining.Skip(stopIndex).ToList();
                if (!StopReachability.CanReach(approach, _truck.PositionOnCurrentEdge, _truck,
                        driver.FuelTracker, driver.DrivingTime, _layer._tickDuration))
                    return null;
                var route = new Route();
                foreach (var step in remaining) route.Add(step.Edge, step.DesiredLane);
                RouteDistanceHelper.Discount(route, _truck.PositionOnCurrentEdge);
                var onwardEdges = onward.Select(step => step.Edge).ToList();
                return new StopRoutePlan(route, remaining[stopIndex].Edge.From,
                    StopReachability.EstimateSeconds(approach, _truck.PositionOnCurrentEdge, _truck),
                    Math.Max(0, approach.Sum(e => e.Length) - _truck.PositionOnCurrentEdge),
                    StopReachability.EstimateSeconds(onwardEdges, 0, _truck), onwardEdges.Sum(e => e.Length));
            }
            return null;
        }

        private StopRoutePlan? CreateStopRouteVia(ISpatialNode stopNode, List<EdgeStop> remaining, int insertIndex,
            ISpatialNode insertFromNode, SemiTruckDriver driver,
            Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors,
            out StopRouteRejection? rejection)
        {
            var plan = BuildStopRouteVia(stopNode, remaining, insertIndex, insertFromNode, driver, connectors,
                out var reason);
            rejection = plan == null ? reason : null;
            return plan;
        }

        private StopRoutePlan? BuildStopRouteVia(ISpatialNode stopNode, List<EdgeStop> remaining, int insertIndex,
            ISpatialNode insertFromNode, SemiTruckDriver driver,
            Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>? connectors,
            out StopRouteRejection reason)
        {
            reason = StopRouteRejection.NoApproachRoute;

            var toStop = SemiTruckRouteFinder.FindFastestComplete(_environment, insertFromNode, stopNode,
                _truck, _layer.RemovedEdges);
            if (toStop == null) { reason = StopRouteRejection.NoApproachRoute; return null; }
            var approach = remaining.Take(insertIndex).Select(step => step.Edge)
                .Concat(toStop.Select(step => step.Edge)).ToList();
            if (approach.Any(edge => !SemiTruckRouteFinder.IsUsableEdge(edge, _truck, _layer.RemovedEdges)))
                { reason = StopRouteRejection.InvalidApproach; return null; }
            if (!StopReachability.CanReach(approach, _truck.PositionOnCurrentEdge, _truck,
                    driver.FuelTracker, driver.DrivingTime, _layer._tickDuration))
                { reason = StopRouteRejection.EnergyOrDrivingLimit; return null; }

            Route? onward = null;
            double bestSeconds = double.PositiveInfinity;
            double bestMeters = double.PositiveInfinity;
            double along = 0;
            // Rejoin locally rather than replacing the entire onward corridor.
            for (int rejoinIndex = insertIndex; rejoinIndex <= remaining.Count; rejoinIndex++)
            {
                if (along > SemiTruckDriverConstants.ForwardRejoinLookaheadMeters) break;
                var rejoinNode = rejoinIndex < remaining.Count ? remaining[rejoinIndex].Edge.From : remaining[^1].Edge.To;
                var suffix = remaining.Skip(rejoinIndex).ToList();
                if (suffix.All(step => SemiTruckRouteFinder.IsUsableEdge(step.Edge, _truck, _layer.RemovedEdges)))
                {
                    Route? connector;
                    if (connectors == null || !connectors.TryGetValue((stopNode, rejoinNode), out connector))
                    {
                        connector = SemiTruckRouteFinder.FindFastestComplete(_environment, stopNode, rejoinNode,
                            _truck, _layer.RemovedEdges);
                        if (connectors != null) connectors[(stopNode, rejoinNode)] = connector;
                    }
                    if (connector != null)
                    {
                        var edges = connector.Select(step => step.Edge).Concat(suffix.Select(step => step.Edge)).ToList();
                        double seconds = StopReachability.EstimateSeconds(edges, 0, _truck);
                        double meters = edges.Sum(edge => edge.Length);
                        if (seconds < bestSeconds - 1e-6 ||
                            (Math.Abs(seconds - bestSeconds) <= 1e-6 && meters < bestMeters))
                        {
                            onward = new Route();
                            foreach (var step in connector) onward.Add(step.Edge, step.DesiredLane);
                            foreach (var step in suffix) onward.Add(step.Edge, step.DesiredLane);
                            bestSeconds = seconds;
                            bestMeters = meters;
                        }
                    }
                }
                if (rejoinIndex < remaining.Count) along += remaining[rejoinIndex].Edge.Length;
            }
            if (onward == null) { reason = StopRouteRejection.NoRejoinRoute; return null; }
            var route = new Route();
            foreach (var step in remaining.Take(insertIndex)) route.Add(step.Edge, step.DesiredLane);
            foreach (var step in toStop) route.Add(step.Edge, step.DesiredLane);
            foreach (var step in onward) route.Add(step.Edge, step.DesiredLane);
            RouteDistanceHelper.Discount(route, _truck.PositionOnCurrentEdge);
            return new StopRoutePlan(route, stopNode,
                StopReachability.EstimateSeconds(approach, _truck.PositionOnCurrentEdge, _truck),
                Math.Max(0, approach.Sum(edge => edge.Length) - _truck.PositionOnCurrentEdge),
                bestSeconds, bestMeters);
        }

        public void ApplyStopRoute(StopRoutePlan plan, SemiTruckSteeringHandle steeringHandle,
            SemiTruckDriver driver, Action<ISpatialNode> onSuccess)
        {
            if (_layer.notifyTrucks) _layer.UnregisterTruckFromRoute(driver, steeringHandle.Route);
            steeringHandle.Route = plan.Route;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(driver, steeringHandle.Route);
            onSuccess(plan.StopNode);
        }
    }

    public sealed record StopRoutePlan(Route Route, ISpatialNode StopNode,
        double ApproachSeconds, double ApproachMeters, double RemainingSeconds, double RemainingMeters);

    public sealed record StopRouteSearch(
        Func<double, double, ISpatialNode, StopRoutePlan?> Create,
        Action<StopRoutePlan> Apply,
        Func<string>? ExplainFailure = null);

    public enum StopRouteRejection
    {
        InvalidApproach, UnusableFacility, NoApproachRoute, EnergyOrDrivingLimit, NoRejoinRoute
    }

    /// <summary>
    /// Corrects <see cref="Route.RemainingRouteDistanceToGoal"/> for routes that are replaced while the truck is
    /// already part-way along its current edge (detours to rest/refuel stops).
    /// </summary>
    /// <remarks>
    /// MARS offers no public API for this: <see cref="Route"/> is sealed, <c>Add</c>/<c>InsertFirst</c> only count whole
    /// edges, and the setter of <c>RemainingRouteDistanceToGoal</c> is internal (only <c>SpatialGraphEnvironment.Move</c>
    /// decrements it). A rebuilt route therefore overstates the remaining distance by the distance already travelled on
    /// the first edge. Left uncorrected, <c>GoalReached</c> is never true (the truck stalls on its last edge) and the
    /// end-of-route braking is wrong.
    /// The route cannot simply start at the next node either: <c>SpatialGraphEnvironment.Move</c> re-inserts the entity
    /// at <c>route[0].Edge.From</c> if that edge is not its current one, which teleports the truck.
    /// The internal setter is therefore called via reflection. Depends on MARS internals; if a MARS update renames or
    /// removes the setter, the static initializer fails at startup and the regression test
    /// (<c>RouteDistanceHelperTests</c>) fails.
    /// </remarks>
    internal static class RouteDistanceHelper
    {
        private static readonly System.Reflection.MethodInfo RemainingDistanceSetter =
            typeof(Route).GetProperty(nameof(Route.RemainingRouteDistanceToGoal))?.GetSetMethod(true)
            ?? throw new InvalidOperationException(
                "MARS Route.RemainingRouteDistanceToGoal setter not found - see RouteDistanceHelper remarks.");

        /// <summary>Reduces the remaining route distance by <paramref name="distance"/> metres (not below 0).</summary>
        public static void Discount(Route route, double distance)
        {
            RemainingDistanceSetter.Invoke(route,
                new object[] { Math.Max(route.RemainingRouteDistanceToGoal - distance, 0.0) });
        }
    }

    public enum StopType
    {
        Rest,
        Refuel
    }
}
