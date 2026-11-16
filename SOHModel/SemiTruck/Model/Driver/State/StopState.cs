using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;
using SOHModel.SemiTruck.Model.Driver.Services;

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

        // A failed route only excludes that approach to the site.
        private readonly HashSet<dynamic> _unreachableFacilities = new();
        private dynamic _lastListFacility;
        private readonly HashSet<(string Location, ISpatialNode Approach)> _rejectedApproaches = new();
        private ISpatialNode? _lastApproachNode;
        protected double FacilityLat;
        protected double FacilityLon;
        public bool PlanningFailed { get; private set; }
        public bool IsPausing => _isPausing;
        public ISpatialNode? TargetNode => _goingToStop ? _targetNode : null;

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

        // Set when planning starts: how far ahead the stop may be placed; null = take the first facility found.
        private double? _reachLimitMeters;

        /// <summary>
        /// Distance ahead up to which the truck can still reach a stop. If set, the farthest facility within it is
        /// chosen (as late as possible), otherwise the nearest one.
        /// </summary>
        protected virtual double? GetReachLimitMeters(SemiTruck truck, FuelConsumptionTracker fuelTracker) => null;

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

        public bool IsAtTarget(SemiTruckSteeringHandle steeringHandle) =>
            _goingToStop && HasArrivedAtStop(steeringHandle);

        public void ValidateApproach(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker, DrivingTimeTracker drivingTime)
        {
            if (!_goingToStop || IsAtTarget(steeringHandle)) return;
            var edges = new List<ISpatialEdge>();
            foreach (var stop in steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops))
            {
                edges.Add(stop.Edge);
                if (stop.Edge.To != _targetNode) continue;
                if (!StopReachability.CanReach(edges, truck.PositionOnCurrentEdge, truck, fuelTracker,
                        drivingTime, layer._tickDuration)) CancelPlanned();
                return;
            }
            CancelPlanned(); // a bypass removed the planned facility
        }

        /// <summary>
        /// Handles the pause logic when the truck is at a stop.
        /// Template method that orchestrates the pause workflow.
        /// </summary>
        private ISpatialNode? _parkedAtNode;

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
                PostgresDbLogger.Instance?.Log(new RestEntity(truck.ID,
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
                // A parked truck sits in a bay of the facility, not in the lane: take it out of the traffic so that
                // trucks passing by or stopping at the same facility neither queue behind it nor see it as occupied.
                _parkedAtNode = _targetNode;
                steeringHandle.Stop();
                truck.Environment.Remove(truck);
                return true;
            }

            // Truck is currently pausing - let the subclass progress its process, then check if it is completed
            if (_isPausing)
            {
                OnPauseTick(layer, truck, fuelTracker, driver);

                if (IsPauseCompleted(layer, truck))
                {
                    if (_parkedAtNode != null)
                    {
                        truck.Environment.Insert(truck, _parkedAtNode);
                        _parkedAtNode = null;
                    }
                    var stopType = GetStopType(); // the subclass may reset the stop kind in OnPauseCompleted
                    Console.WriteLine($"[{stopType}] Pause completed. Resuming route.");
                    OnPauseCompleted(layer, truck, fuelTracker, driver);
                    driver.DrivingTime.RegisterStop(layer._simulationTime - _stopStartTime);

                    // logged after OnPauseCompleted so that the event carries the final fuel level
                    PostgresDbLogger.Instance?.Log(new RestEntity(truck.ID,
                        layer.Context.CurrentTick,
                        stopType,
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
            SemiTruck truck, FuelConsumptionTracker fuelTracker, Action<double, double, ISpatialNode> planStopCallback,
            DrivingTimeTracker? drivingTime = null)
        {
            // Skip if already planned or pausing
            if (_planned || _goingToStop || _isPausing)
                return;

            // Check if stop is needed (subclass-specific logic)
            if (!ShouldStop(steeringHandle, layer, truck, fuelTracker))
                return;

            double legalReach = drivingTime == null ? double.PositiveInfinity :
                Math.Max(0, (drivingTime.LegalDrivingLeft - layer._tickDuration).TotalSeconds) *
                Math.Max(1, truck.MaxSpeed * SemiTruckDriverConstants.PlanningSpeedFraction);
            _reachLimitMeters = Math.Min(GetReachLimitMeters(truck, fuelTracker) ?? double.PositiveInfinity,
                Math.Min(legalReach, fuelTracker.PlanningRangeKm(truck) * 1000));
            PlanningFailed = false;

            // Condition met → plan the stop
            PostgresDbLogger.Instance?.Log(new RestEntity(truck.ID, layer.Context.CurrentTick, GetStopType(),
                RestEventType.Planned, steeringHandle.Position.Latitude, steeringHandle.Position.Longitude,
                fuelTracker.FuelCarrierAmount, steeringHandle.Route.RemainingRouteDistanceToGoal));
            // Failed routed candidates are blacklisted by CancelPlanned; try the next one before moving.
            do
            {
                PlanStop(steeringHandle, layer, planStopCallback);
            } while (!IsActive && !PlanningFailed);
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
            var searchRadius = _reachLimitMeters.HasValue
                ? Math.Min(GetSearchRadius(), _reachLimitMeters.Value)
                : GetSearchRadius();
            var searchTags = GetSearchTags();
            (double Lat, double Lon, ISpatialNode Node)? farthest = null;

            // Search along the route for a facility
            // Only look ahead: skip passed edges, and for the edge the truck is on only consider its end node,
            // otherwise the detour would be inserted behind the truck.
            bool isCurrentEdge = true;
            foreach (var routeStep in steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops))
            {
                var edge = routeStep.Edge;
                accumulatedDistance += isCurrentEdge && steeringHandle.RemainingDistanceOnEdge >= 0
                    ? steeringHandle.RemainingDistanceOnEdge : edge.Length;
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
                                if (_rejectedApproaches.Contains((LocationKey(facilityCoord.Latitude, facilityCoord.Longitude), node)))
                                    continue;
                                if (!_reachLimitMeters.HasValue)
                                {
                                    OnFacilityChosen(null);
                                    _lastListFacility = null;
                                    FacilityLat = facilityCoord.Latitude;
                                    FacilityLon = facilityCoord.Longitude;
                                    _lastApproachNode = node;
                                    planStopCallback(facilityCoord.Latitude, facilityCoord.Longitude, node);
                                    return;
                                }
                                farthest = (facilityCoord.Latitude, facilityCoord.Longitude, node);
                                goto nextStep;
                            }
                        }
                    }
                }
                nextStep:;
            }

            if (farthest.HasValue)
            {
                OnFacilityChosen(null);
                _lastListFacility = null;
                FacilityLat = farthest.Value.Lat;
                FacilityLon = farthest.Value.Lon;
                _lastApproachNode = farthest.Value.Node;
                planStopCallback(farthest.Value.Lat, farthest.Value.Lon, farthest.Value.Node);
                return;
            }

            // No facility found on the way → fallback to external list
            Console.WriteLine($"[{GetStopType()}] No facility found along route – fallback to CSV list.");
            FindNearestFromList(steeringHandle, layer, planStopCallback);
        }

        /// <summary>
        /// Called when a stop location has been chosen; <paramref name="facility"/> is the entry of the facility list,
        /// or <c>null</c> if the facility comes from the map (tagged edge) and has no further data.
        /// </summary>
        protected virtual void OnFacilityChosen(dynamic facility)
        {
        }

        /// <summary>
        /// Finds nearest facility from external CSV list.
        /// </summary>
        protected void FindNearestFromList(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            var facilityList = GetFacilityList(layer);

            // Rank by detour, not by distance to the truck: a facility behind the truck or far off the route would send
            // it on a loop. Cost = distance along the route to the closest route node ahead + twice the way off the route.
            dynamic nearest = null;
            ISpatialNode insertFromNode = null;
            double bestCost = double.MaxValue;
            if (facilityList != null)
            {
                var nodes = new List<(ISpatialNode Node, double Along)>();
                double along = 0;
                bool first = true;
                foreach (var step in steeringHandle.Route.Stops.Skip(steeringHandle.Route.PassedStops))
                {
                    along += first && steeringHandle.RemainingDistanceOnEdge >= 0
                        ? steeringHandle.RemainingDistanceOnEdge : step.Edge.Length;
                    first = false;
                    if (along > (_reachLimitMeters.HasValue
                            ? Math.Min(GetSearchRadius(), _reachLimitMeters.Value) : GetSearchRadius())) break;
                    nodes.Add((step.Edge.To, along));
                }

                foreach (var facility in facilityList)
                {
                    if (_unreachableFacilities.Contains(facility)) continue;
                    foreach (var (node, alongRoute) in nodes)
                    {
                        if (_rejectedApproaches.Contains((LocationKey(facility.Lat, facility.Lon), node))) continue;
                        double offRoute = GeometryHelper.ApproxDistanceMeters(
                            facility.Lat, facility.Lon, node.Position.Latitude, node.Position.Longitude);
                        if (offRoute > SemiTruckDriverConstants.MaxFacilityDetourMeters ||
                            (_reachLimitMeters.HasValue && alongRoute + offRoute > _reachLimitMeters.Value)) continue;
                        // with a reach limit the farthest facility is preferred: the detour is a penalty, not the distance
                        double cost = _reachLimitMeters.HasValue && offRoute <= SemiTruckDriverConstants.MaxFacilityDetourMeters
                            ? -alongRoute + 2 * offRoute
                            : alongRoute + 2 * offRoute;
                        if (cost >= bestCost) continue;
                        bestCost = cost;
                        nearest = facility;
                        insertFromNode = node;
                    }
                }

            }

            if (nearest != null)
            {
                Console.WriteLine($"Using fallback {GetStopType()} from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                OnFacilityChosen(nearest);
                _lastListFacility = nearest;
                FacilityLat = nearest.Lat;
                FacilityLon = nearest.Lon;
                _lastApproachNode = insertFromNode;
                planStopCallback(nearest.Lat, nearest.Lon, insertFromNode);
            }
            else
            {
                Console.WriteLine($"No fallback {GetStopType()} found in external list.");
                PlanningFailed = true;
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
            if (_lastApproachNode != null)
                _rejectedApproaches.Add((LocationKey(FacilityLat, FacilityLon), _lastApproachNode));
            _lastListFacility = null;
            _lastApproachNode = null;
            _goingToStop = false;
            _planned = false;
        }

        protected void ResetFacilitySearch()
        {
            PlanningFailed = false;
            _rejectedApproaches.Clear();
            _unreachableFacilities.Clear();
        }

        private static string LocationKey(double lat, double lon) =>
            FormattableString.Invariant($"{lat:F6}:{lon:F6}");

        /// <summary>
        /// Sets the target stop node.
        /// </summary>
        public void SetTargetNode(ISpatialNode node)
        {
            _targetNode = node;
        }
    }
}
