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
    /// Keeps the route of a SemiTruckDriver drivable by bypassing removed edges ahead. Stop routes are planned by
    /// <see cref="StopRoutePlanner"/>.
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
}
