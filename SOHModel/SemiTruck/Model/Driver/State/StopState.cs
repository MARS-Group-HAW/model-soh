using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Abstract base class for managing stop-related state and logic (rest, refuel, etc.)
    /// Implements the template method pattern to share common pause and planning behavior.
    /// </summary>
    public abstract class StopState
    {
        protected bool _isPausing;
        protected bool _planned;
        protected bool _goingToStop;
        protected ISpatialNode _targetNode;
        protected DateTime _stopStartTime;

        /// <summary>True while a stop is planned, being approached or in progress.</summary>
        public bool IsActive => _planned || _goingToStop || _isPausing;

        /// <summary>
        /// Gets the type of stop for logging purposes.
        /// </summary>
        protected abstract RestStateType GetStopType();

        /// <summary>
        /// Calculates the duration of the pause at this stop.
        /// </summary>
        protected abstract TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck);

        /// <summary>
        /// Returns the facility tags to search for along the route.
        /// </summary>
        protected abstract string[] GetSearchTags();

        /// <summary>
        /// Returns the list of fallback facilities from the layer.
        /// </summary>
        protected abstract IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer);

        /// <summary>
        /// Determines whether a stop is needed based on subclass-specific conditions.
        /// Called by CheckAndPlan() to decide if facility search should be triggered.
        /// </summary>
        protected abstract bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker);

        /// <summary>
        /// Called when the pause is completed - allows subclasses to perform cleanup.
        /// </summary>
        protected abstract void OnPauseCompleted(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver);

        /// <summary>
        /// Gets the search radius for this stop type.
        /// </summary>
        protected abstract double GetSearchRadius();

        /// <summary>
        /// Determines if arrival condition is met (subclasses can override for custom logic).
        /// </summary>
        protected virtual bool HasArrivedAtStop(SemiTruckSteeringHandle steeringHandle)
        {
            if (steeringHandle.Position == null || _targetNode == null)
                return false;

            if (GeometryHelper.IsOnNode(steeringHandle.Position, _targetNode))
                return true;

            // At highway speed the truck moves much further than the node tolerance per tick and can skip over the
            // stop node. Having entered the edge that leaves the stop node (the return leg) means it was reached.
            return steeringHandle.Route.Count > 0 &&
                   GeometryHelper.IsOnNode(_targetNode.Position, steeringHandle.Route[0].Edge.From);
        }

        /// <summary>
        /// Handles the pause logic when the truck is at a stop.
        /// Template method that orchestrates the pause workflow.
        /// </summary>
        public bool HandlePause(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            // Truck just arrived at the stop → begin pause
            if (_goingToStop && HasArrivedAtStop(steeringHandle))
            {
                var pauseDuration = GetPauseDuration(layer, truck);
                Console.WriteLine(pauseDuration > TimeSpan.Zero
                    ? $"[{GetStopType()}] Arrived at stop. Starting pause ({pauseDuration.TotalMinutes:F0} min)."
                    : $"[{GetStopType()}] Arrived at stop. Starting pause (duration depends on the process).");
                PostgresDbLogger.Instance.Log(new RestEntity(truck.ID,
                    layer.Context.CurrentTick,
                    GetStopType(),
                    RestEventType.Start,
                    driver.Position.Latitude,
                    driver.Position.Longitude, 
                    driver.FuelCarrierAmount,
                    driver.RemainingRouteDistanceToGoal
                    )
                );
                _stopStartTime = layer._simulationTime;
                OnArrival(layer);
                _goingToStop = false;
                _isPausing = true;
                return true;
            }

            // Truck is currently pausing - let the subclass progress its process, then check if it is completed
            if (_isPausing)
            {
                OnPauseTick(layer, truck, fuelTracker, driver);

                if (IsPauseCompleted(layer, truck))
                {
                    Console.WriteLine($"[{GetStopType()}] Pause completed. Resuming route.");
                    OnPauseCompleted(layer, truck, fuelTracker, driver);
                    driver.DrivingTime.RegisterStop(layer._simulationTime - _stopStartTime);

                    // logged after OnPauseCompleted so that the event carries the final fuel level
                    PostgresDbLogger.Instance.Log(new RestEntity(truck.ID,
                        layer.Context.CurrentTick,
                        GetStopType(),
                        RestEventType.End,
                        driver.Position.Latitude,
                        driver.Position.Longitude,
                        driver.FuelCarrierAmount,
                        driver.RemainingRouteDistanceToGoal
                        )
                    );

                    _planned = false;
                    _targetNode = null;
                    _isPausing = false;
                    return false;
                }
            }

            // Truck is still pausing
            return _isPausing;
        }

        /// <summary>
        /// Hook called on every tick while the truck is pausing, before <see cref="IsPauseCompleted"/> is evaluated.
        /// </summary>
        protected virtual void OnPauseTick(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            // Default: nothing happens while pausing
        }

        /// <summary>
        /// Hook called when arriving at the stop (before pause begins).
        /// </summary>
        protected virtual void OnArrival(SemiTruckLayer layer)
        {
            // Default: no additional action
        }

        /// <summary>
        /// Determines if the pause is completed based on subclass-specific logic.
        /// Called during HandlePause() to check if the pause should end.
        /// </summary>
        protected abstract bool IsPauseCompleted(SemiTruckLayer layer, SemiTruck truck);

        /// <summary>
        /// Checks if a stop is needed and plans route to facility if so.
        /// Template method that orchestrates: already-planned check → condition check → facility search.
        /// </summary>
        public void CheckAndPlan(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker, Action<double, double, ISpatialNode> planStopCallback)
        {
            // Skip if already planned or pausing
            if (_planned || _goingToStop || _isPausing)
                return;

            // Check if stop is needed (subclass-specific logic)
            if (!ShouldStop(steeringHandle, layer, truck, fuelTracker))
                return;

            // Condition met → plan the stop
            PostgresDbLogger.Instance?.Log(new RestEntity(truck.ID, layer.Context.CurrentTick, GetStopType(),
                RestEventType.Planned, steeringHandle.Position.Latitude, steeringHandle.Position.Longitude,
                fuelTracker.FuelCarrierAmount, steeringHandle.Route.RemainingRouteDistanceToGoal));
            PlanStop(steeringHandle, layer, planStopCallback);
        }

        /// <summary>
        /// Plans route to facility. Does not check conditions - assumes caller has already determined a stop is needed.
        /// Called internally by CheckAndPlan() after condition check passes.
        /// </summary>
        protected void PlanStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            // Skip if already planned or pausing
            if (_planned || _goingToStop || _isPausing)
                return;

            double accumulatedDistance = 0;
            var searchRadius = GetSearchRadius();
            var searchTags = GetSearchTags();

            // Search along the route for a facility
            // Only look ahead: skip passed edges, and for the edge the truck is on only consider its end node,
            // otherwise the detour would be inserted behind the truck.
            bool isCurrentEdge = true;
            foreach (var routeStep in steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops))
            {
                var edge = routeStep.Edge;
                accumulatedDistance += edge.Length;
                if (accumulatedDistance > searchRadius)
                    break;

                var nodesToCheck = isCurrentEdge ? new[] { edge.To } : new[] { edge.From, edge.To };
                isCurrentEdge = false;

                foreach (ISpatialNode node in nodesToCheck)
                {
                    foreach (var connectedEdge in node.OutgoingEdges.Values)
                    {
                        if (connectedEdge.Attributes.TryGetValue("source_tag", out var tag))
                        {
                            var tagStr = tag?.ToString()?.ToLowerInvariant();
                            if (searchTags.Contains(tagStr))
                            {
                                var facilityCoord = connectedEdge.To.Position;
                                planStopCallback(facilityCoord.Latitude, facilityCoord.Longitude, node);
                                return;
                            }
                        }
                    }
                }
            }

            // No facility found on the way → fallback to external list
            Console.WriteLine($"[{GetStopType()}] No facility found along route – fallback to CSV list.");
            FindNearestFromList(steeringHandle, layer, planStopCallback);
        }

        /// <summary>Whether the stop may take place at a plain route node when no facility is close by.</summary>
        protected virtual bool AllowRoadsideStop => false;

        /// <summary>
        /// Finds nearest facility from external CSV list.
        /// </summary>
        protected void FindNearestFromList(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            var currentLat = steeringHandle.Position.Latitude;
            var currentLon = steeringHandle.Position.Longitude;

            var facilityList = GetFacilityList(layer);

            // Rank by detour, not by distance to the truck: a facility behind the truck or far off the route would send
            // it on a loop. Cost = distance along the route to the closest route node ahead + twice the way off the route.
            dynamic nearest = null;
            ISpatialNode insertFromNode = null;
            double bestCost = double.MaxValue;
            double bestOffRoute = double.MaxValue;
            if (facilityList != null)
            {
                var nodes = new List<(ISpatialNode Node, double Along)>();
                double along = 0;
                foreach (var step in steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops))
                {
                    along += step.Edge.Length;
                    if (along > GetSearchRadius()) break;
                    nodes.Add((step.Edge.To, along));
                }

                foreach (var facility in facilityList)
                {
                    foreach (var (node, alongRoute) in nodes)
                    {
                        double offRoute = GeometryHelper.ApproxDistanceMeters(
                            facility.Lat, facility.Lon, node.Position.Latitude, node.Position.Longitude);
                        double cost = alongRoute + 2 * offRoute;
                        if (cost >= bestCost) continue;
                        bestCost = cost;
                        bestOffRoute = offRoute;
                        nearest = facility;
                        insertFromNode = node;
                    }
                }

                // The facility lists are sparse; a driver would rather pull over at the roadside than drive a long loop.
                if (AllowRoadsideStop && bestOffRoute > SemiTruckDriverConstants.MaxFacilityDetourMeters)
                {
                    // the stop node needs a following edge on the route, so the last node is excluded
                    var candidates = nodes.Take(nodes.Count - 1).ToList();
                    var roadside = candidates.FirstOrDefault(n => n.Along >= SemiTruckDriverConstants.RoadsideStopLookaheadMeters);
                    var node = roadside.Node ?? candidates.LastOrDefault().Node;
                    if (node != null)
                    {
                        Console.WriteLine($"[{GetStopType()}] No facility close to the route – stopping at the roadside.");
                        planStopCallback(node.Position.Latitude, node.Position.Longitude, node);
                        return;
                    }
                }

                // nothing within reach ahead: fall back to the facility closest to the truck
                if (nearest == null)
                {
                    nearest = facilityList.MinBy(r =>
                        GeometryHelper.GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));
                    insertFromNode = steeringHandle.Route.Stops[steeringHandle.Route.PassedStops].Edge.To;
                }
            }

            if (nearest != null)
            {
                Console.WriteLine($"Using fallback {GetStopType()} from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                planStopCallback(nearest.Lat, nearest.Lon, insertFromNode);
            }
            else
            {
                Console.WriteLine($"No fallback {GetStopType()} found in external list.");
            }
        }

        /// <summary>
        /// Marks that a stop has been planned.
        /// </summary>
        public void MarkPlanned()
        {
            _planned = true;
            _goingToStop = true;
        }

        /// <summary>
        /// Cancels stop planning.
        /// </summary>
        public void CancelPlanned()
        {
            _goingToStop = false;
            _planned = false;
        }

        /// <summary>
        /// Sets the target stop node.
        /// </summary>
        public void SetTargetNode(ISpatialNode node)
        {
            _targetNode = node;
        }
    }
}
