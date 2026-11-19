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
        public string? PlanningFailureDetails { get; private set; }
        public Guid? PhaseId { get; private set; }
        private Guid ActivePhaseId => PhaseId ??
            throw new InvalidOperationException("A stop event requires an active phase.");
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

        // Set when planning starts: how far ahead the stop may be placed.
        private double _reachLimitMeters;

        /// <summary>
        /// Distance ahead up to which this stop type wants its stop, in addition to the energy and legal driving
        /// limits; <c>null</c> for no further limit.
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

            // At highway speed the truck can skip over the stop node within one tick. Having entered the edge
            // that leaves the stop node means it was reached.
            var route = steeringHandle.Route;
            return route.PassedStops < route.Count && route.PassedStops > 0 &&
                   route.Stops[route.PassedStops].Edge.From == _targetNode;
        }

        public bool IsAtTarget(SemiTruckSteeringHandle steeringHandle) =>
            _goingToStop && HasArrivedAtStop(steeringHandle);

        public void ValidateApproach(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker, DrivingTimeTracker drivingTime)
        {
            if (!_goingToStop || IsAtTarget(steeringHandle)) return;
            var edges = new List<ISpatialEdge>();
            foreach (var stop in steeringHandle.RemainingSteps)
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
                LogRestEvent(new RestEntity(truck.ID,
                    ActivePhaseId, layer.Context.CurrentTick, GetStopType(), RestEventType.Start, driver.Position.Latitude, driver.Position.Longitude, driver.FuelCarrierAmount, driver.RemainingRouteDistanceToGoal)
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
                    ReturnToRoad(layer, truck, steeringHandle, driver);
                    var stopType = GetStopType(); // the subclass may reset the stop kind in OnPauseCompleted
                    OnPauseCompleted(layer, truck, fuelTracker, driver);
                    driver.DrivingTime.RegisterStop(layer._simulationTime - _stopStartTime);

                    // logged after OnPauseCompleted so that the event carries the final fuel level
                    LogRestEvent(new RestEntity(truck.ID,
                        ActivePhaseId, layer.Context.CurrentTick, stopType, RestEventType.End, driver.Position.Latitude, driver.Position.Longitude, driver.FuelCarrierAmount, driver.RemainingRouteDistanceToGoal)
                    );

                    _planned = false;
                    _targetNode = null;
                    _isPausing = false;
                    PhaseId = null;
                    return false;
                }

            }

            // Truck is still pausing
            return _isPausing;
        }

        protected void ReturnToRoad(SemiTruckLayer layer, SemiTruck truck, SemiTruckSteeringHandle steeringHandle,
            SemiTruckDriver driver)
        {
            if (_parkedAtNode == null) return;
            ContinueFromNode(steeringHandle, layer, driver, _parkedAtNode);
            truck.Environment.Insert(truck, _parkedAtNode);
            _parkedAtNode = null;
        }

        /// <summary>
        /// The truck arrives at the end of the edge leading to the stop node, so that edge is still the current one.
        /// Drop it, so that the truck re-enters the road on the edge leaving the stop node instead of driving it twice.
        /// </summary>
        internal static void ContinueFromNode(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruckDriver driver, ISpatialNode node)
        {
            var remaining = steeringHandle.RemainingSteps.ToList();
            int index = remaining.FindIndex(stop => stop.Edge.From == node);
            if (index <= 0) return;
            var route = new Route();
            foreach (var stop in remaining.Skip(index)) route.Add(stop.Edge, stop.DesiredLane);
            layer.ReplaceRoute(driver, steeringHandle, route);
        }

        protected virtual void LogRestEvent(RestEntity entry) => PostgresDbLogger.Instance?.Log(entry);

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
        /// Template method that orchestrates: already-planned check -> condition check -> facility search.
        /// </summary>
        public void CheckAndPlan(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker, DrivingTimeTracker drivingTime, StopRouteSearch routing)
        {
            // Skip if already planned or pausing
            if (_planned || _goingToStop || _isPausing)
                return;

            // Check if stop is needed (subclass-specific logic)
            if (!ShouldStop(steeringHandle, layer, truck, fuelTracker))
                return;

            double legalReach = Math.Max(0, (drivingTime.LegalDrivingLeft - layer._tickDuration).TotalSeconds) *
                PlanningSpeed.Conservative(truck);
            _reachLimitMeters = Math.Min(GetReachLimitMeters(truck, fuelTracker) ?? double.PositiveInfinity,
                Math.Min(legalReach, fuelTracker.PlanningRangeKm(truck) * 1000));
            PlanningFailed = false;
            PlanningFailureDetails = null;

            PlanBestStop(steeringHandle, layer, truck, fuelTracker, drivingTime, routing);

            if (_goingToStop)
                LogRestEvent(new RestEntity(truck.ID, ActivePhaseId, layer.Context.CurrentTick, GetStopType(), RestEventType.Planned, steeringHandle.Position.Latitude, steeringHandle.Position.Longitude, fuelTracker.FuelCarrierAmount, steeringHandle.Route.RemainingRouteDistanceToGoal));
        }

        /// <summary>Energy reserve (as distance) a candidate should leave on arrival; preferred, not required.</summary>
        protected virtual double ArrivalReserveMeters() => 0;

        protected virtual (double Seconds, double Charge) EstimateStopCost(StopRoutePlan plan,
            SemiTruckLayer layer, SemiTruck truck, FuelConsumptionTracker fuel, object? facility) =>
            (GetPauseDuration(layer, truck).TotalSeconds, 0);

        /// <summary>
        /// Remaining route distance (to the goal) at the farthest route node within <paramref name="reachMeters"/>
        /// that has a facility of this type within the detour tolerance; <c>null</c> if there is none.
        /// </summary>
        internal double? LastApproachRemaining(SemiTruckSteeringHandle steering, SemiTruckLayer layer,
            double reachMeters)
        {
            var facilities = GetFacilityList(layer).Select(f => ((double)f.Lat, (double)f.Lon)).ToList();
            var tags = GetSearchTags();
            double remaining = steering.Route.RemainingRouteDistanceToGoal;
            double? last = null;
            foreach (var (step, along) in steering.StepsAhead())
            {
                if (along > reachMeters) break;
                var node = step.Edge.To;
                double lat = node.Position.Latitude;
                double lon = node.Position.Longitude;
                bool tagged = node.OutgoingEdges.Values.Any(edge =>
                    edge.Attributes.TryGetValue("source_tag", out var tag) &&
                    tags.Contains(tag?.ToString()?.ToLowerInvariant()));
                // The degree pre-filter (> 10 km in both directions at German latitudes) keeps this cheap.
                if (tagged || facilities.Any(f => Math.Abs(f.Item1 - lat) < 0.1 && Math.Abs(f.Item2 - lon) < 0.2 &&
                        GeometryHelper.ApproxDistanceMeters(f.Item1, f.Item2, lat, lon) <=
                        SemiTruckDriverConstants.MaxFacilityDetourMeters))
                    last = remaining - along;
            }
            return last;
        }

        private void PlanBestStop(SemiTruckSteeringHandle steering, SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuel, DrivingTimeTracker drivingTime, StopRouteSearch routing,
            bool extendSearch = false)
        {
            double horizon = extendSearch ? _reachLimitMeters : Math.Min(GetSearchRadius(), _reachLimitMeters);
            var nodes = steering.StepsAhead().TakeWhile(ahead => ahead.Along <= horizon)
                .Select(ahead => ahead.Step.Edge.To).ToList();

            var candidates = new Dictionary<(string Location, ISpatialNode Node),
                (double Lat, double Lon, object? Facility)>();
            void Add(double lat, double lon, ISpatialNode node, object? facility)
            {
                var location = LocationKey(lat, lon);
                if (_rejectedApproaches.Contains((location, node))) return;
                candidates[(location, node)] = (lat, lon, facility);
            }

            var tags = GetSearchTags();
            foreach (var node in nodes)
                foreach (var edge in node.OutgoingEdges.Values)
                    if (edge.Attributes.TryGetValue("source_tag", out var tag) &&
                        tags.Contains(tag?.ToString()?.ToLowerInvariant()))
                        Add(edge.To.Position.Latitude, edge.To.Position.Longitude, node, null);
            foreach (var facility in GetFacilityList(layer))
            {
                if (_unreachableFacilities.Contains(facility)) continue;
                double lat = facility.Lat;
                double lon = facility.Lon;
                foreach (var node in nodes)
                    if (GeometryHelper.ApproxDistanceMeters(lat, lon,
                            node.Position.Latitude, node.Position.Longitude) <=
                        SemiTruckDriverConstants.MaxFacilityDetourMeters)
                        Add(lat, lon, node, facility);
            }

            StopRoutePlan? best = null;
            (double Lat, double Lon, object? Facility) chosen = default;
            ISpatialNode? chosenApproach = null;
            double bestSeconds = double.PositiveInfinity;
            double bestCharge = double.PositiveInfinity;
            bool bestUsesReserve = true;
            double rangeMeters = fuel.PlanningRangeKm(truck) * 1000;
            foreach (var (key, candidate) in candidates)
            {
                var plan = routing.Create(candidate.Lat, candidate.Lon, key.Node);
                if (plan == null) continue;
                var cost = EstimateStopCost(plan, layer, truck, fuel, candidate.Facility);
                double seconds = plan.Approach.Seconds + plan.Onward.Seconds + cost.Seconds +
                    drivingTime.EstimateFurtherRestSeconds(plan.Approach.Seconds, cost.Seconds, plan.Onward.Seconds);
                // Candidates keeping the arrival reserve come first: consumption may exceed the estimate on the way.
                bool usesReserve = plan.Approach.Meters + ArrivalReserveMeters() > rangeMeters;
                if (usesReserve && !bestUsesReserve) continue;
                if (usesReserve == bestUsesReserve && (seconds > bestSeconds + 1e-6 ||
                    (Math.Abs(seconds - bestSeconds) <= 1e-6 &&
                     (cost.Charge > bestCharge + 1e-6 ||
                      (Math.Abs(cost.Charge - bestCharge) <= 1e-6 &&
                       best != null && plan.Approach.Seconds <= best.Approach.Seconds))))) continue;
                best = plan;
                bestUsesReserve = usesReserve;
                chosen = candidate;
                chosenApproach = key.Node;
                bestSeconds = seconds;
                bestCharge = cost.Charge;
            }
            if (best == null)
            {
                if (!extendSearch && _reachLimitMeters > horizon)
                {
                    PlanBestStop(steering, layer, truck, fuel, drivingTime, routing, true);
                    return;
                }
                PlanningFailed = true;
                var rejections = routing.ExplainFailure?.Invoke();
                PlanningFailureDetails = $"{candidates.Count} candidate approaches in a {horizon / 1000:F1} km window." +
                    (string.IsNullOrEmpty(rejections) ? "" : $" Rejections: {rejections}");
                return;
            }
            OnFacilityChosen(chosen.Facility);
            FacilityLat = chosen.Lat;
            FacilityLon = chosen.Lon;
            _lastListFacility = chosen.Facility;
            _lastApproachNode = chosenApproach;
            routing.Apply(best);
        }

        /// <summary>
        /// Called when a stop location has been chosen; <paramref name="facility"/> is the entry of the facility list,
        /// or <c>null</c> if the facility comes from the map (tagged edge) and has no further data.
        /// </summary>
        protected virtual void OnFacilityChosen(dynamic facility)
        {
        }

        /// <summary>
        /// Marks that a stop has been planned.
        /// </summary>
        public void MarkPlanned()
        {
            if (!_planned) PhaseId = Guid.NewGuid();
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
            PhaseId = null;
        }

        protected void ResetFacilitySearch()
        {
            PlanningFailed = false;
            PlanningFailureDetails = null;
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
