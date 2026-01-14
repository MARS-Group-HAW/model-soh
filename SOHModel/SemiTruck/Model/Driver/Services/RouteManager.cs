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

        private Route _originalRoute;

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
            // Unregister from current route tracking if applicable
            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(driver, steeringHandle.Route);

            // Backup current route
            _originalRoute = new Route();
            foreach (var stop in steeringHandle.Route)
                _originalRoute.Add(stop.Edge, stop.DesiredLane);

            // Identify position to split the route
            var insertIndex = steeringHandle.Route.Stops
                .Skip(steeringHandle.Route.PassedStops)
                .ToList()
                .FindIndex(stop => stop.Edge.From == insertFromNode);

            if (insertIndex == -1)
            {
                Console.WriteLine("Could not find valid insertion point for detour.");
                onFailure();
                return;
            }

            var splitStop = steeringHandle.Route.Stops[steeringHandle.Route.PassedStops + insertIndex];
            var splitNode = (splitStop.Edge.From == insertFromNode) ? splitStop.Edge.From : splitStop.Edge.To;

            // Build route to stop location
            var toStopRoute = SemiTruckRouteFinder.Find(
                _environment, _driveMode,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                targetLat, targetLon,
                null, "", _truck.Height, _truck.Mass,
                _truck.Width, _truck.Length, _truck.MaxIncline,
                _layer.RemovedEdges, false
            );

            // Build return route from stop back to original path
            var backRoute = SemiTruckRouteFinder.Find(
                _environment, _driveMode,
                targetLat, targetLon,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                null, "", _truck.Height, _truck.Mass,
                _truck.Width, _truck.Length, _truck.MaxIncline,
                _layer.RemovedEdges, false
            );

            if (toStopRoute == null || toStopRoute.Count == 0 || backRoute == null || backRoute.Count == 0)
            {
                Console.WriteLine("Failed to compute detour or return route.");
                onFailure();
                return;
            }

            // Remember destination node (done via callback/onSuccess)
            var destinationNode = toStopRoute.Stops[^1].Edge.To;

            // Merge routes: pre-stop Route + detour + return-detour + remaining Route
            var newRoute = new Route();

            for (int i = steeringHandle.Route.PassedStops; i < steeringHandle.Route.PassedStops + insertIndex; i++)
                newRoute.Add(steeringHandle.Route.Stops[i].Edge, steeringHandle.Route.Stops[i].DesiredLane);

            foreach (var stop in toStopRoute)
                newRoute.Add(stop.Edge, stop.DesiredLane);

            foreach (var stop in backRoute)
                newRoute.Add(stop.Edge, stop.DesiredLane);

            for (int i = steeringHandle.Route.PassedStops + insertIndex;
                 i < steeringHandle.Route.Stops.Count;
                 i++)
                newRoute.Add(steeringHandle.Route.Stops[i].Edge, steeringHandle.Route.Stops[i].DesiredLane);

            steeringHandle.Route = newRoute;

            if (_layer.notifyTrucks)
                _layer.RegisterTruckForRoute(driver, steeringHandle.Route);

            Console.WriteLine($"Truck diverts to stop ({stopType}) at ({targetLat}, {targetLon}) and continues.");

            onSuccess(destinationNode);
        }
    }

    public enum StopType
    {
        Rest,
        Refuel
    }
}
