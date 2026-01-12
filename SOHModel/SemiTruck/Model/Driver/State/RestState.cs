using System;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages rest area related state and logic for a SemiTruckDriver.
    /// </summary>
    public class RestState
    {
        private DateTime _lastBreakTime;
        private DateTime _pausedUntilTime = DateTime.MinValue;
        private bool _restAreaPlanned;
        private bool _goingToRestArea;
        private bool _pauseCompleted;
        private ISpatialNode _restNode;
        private readonly TimeSpan _maxDrivingTimeWithoutBreak = SemiTruckDriverConstants.MaxDrivingTimeLimit;

        /// <summary>
        /// Initializes the rest state with the current simulation time.
        /// </summary>
        public void Initialize(DateTime simulationTime)
        {
            _lastBreakTime = simulationTime;
        }

        /// <summary>
        /// Handles the pause logic when the truck is resting.
        /// </summary>
        /// <returns>True if truck is currently resting and simulation tick should be skipped</returns>
        public bool HandlePause(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer)
        {
            // Truck is still in rest pause
            if (_pausedUntilTime > layer._simulationTime)
                return true;

            // Rest time is over → clean up state and continue driving
            if (_pauseCompleted && _pausedUntilTime <= layer._simulationTime)
            {
                Console.WriteLine("Rest pause completed. Resuming original route.");
                _restAreaPlanned = false;
                _pauseCompleted = false;
                _restNode = null;
            }

            // Truck just arrived at the rest area → begin pause
            if (_goingToRestArea &&
                steeringHandle.Position != null &&
                _restNode != null &&
                GeometryHelper.IsOnNode(steeringHandle.Position, _restNode))
            {
                Console.WriteLine("Arrived at rest area. Starting pause.");
                _pausedUntilTime = layer._simulationTime + SemiTruckDriverConstants.DefaultRestDuration;
                _lastBreakTime = layer._simulationTime;
                _goingToRestArea = false;
                _pauseCompleted = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the truck needs to take a mandatory rest break.
        /// </summary>
        public bool ShouldRest(DateTime simulationTime, double remainingDistance)
        {
            return (simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                   remainingDistance > SemiTruckDriverConstants.RestAreaSearchRadius;
        }

        /// <summary>
        /// Checks and plans route to rest area if needed.
        /// </summary>
        public void CheckAndPlanRest(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            // Skip if a rest area is already being approached or pause is scheduled
            if (_restAreaPlanned || _goingToRestArea || _pausedUntilTime > layer._simulationTime)
                return;

            if (!ShouldRest(layer._simulationTime, steeringHandle.Route.RemainingRouteDistanceToGoal))
                return;

            double accumulatedDistance = 0;

            // Search next 100 km for a rest area directly along the route
            foreach (var routeStep in steeringHandle.Route)
            {
                var edge = routeStep.Edge;
                accumulatedDistance += edge.Length;
                if (accumulatedDistance > SemiTruckDriverConstants.RestAreaSearchRadius)
                    break;

                var nodesToCheck = new[] { edge.From, edge.To };

                foreach (ISpatialNode node in nodesToCheck)
                {
                    foreach (var connectedEdge in node.OutgoingEdges.Values)
                    {
                        if (connectedEdge.Attributes.TryGetValue("source_tag", out var tag))
                        {
                            var tagStr = tag?.ToString()?.ToLowerInvariant();
                            if (tagStr == "rest_area" || tagStr == "services")
                            {
                                var restCoord = connectedEdge.To.Position;
                                planStopCallback(restCoord.Latitude, restCoord.Longitude, node);
                                return;
                            }
                        }
                    }
                }
            }

            // No nearby OSM rest area found → fallback to predefined list
            FindNearestFromList(steeringHandle, layer, planStopCallback);
        }

        /// <summary>
        /// Finds nearest rest area from external CSV list.
        /// </summary>
        private void FindNearestFromList(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            var currentLat = steeringHandle.Position.Latitude;
            var currentLon = steeringHandle.Position.Longitude;

            var nearest = layer.AllRestAreas.MinBy(r =>
                GeometryHelper.GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));

            if (nearest != null)
            {
                Console.WriteLine($"Using fallback rest area from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                planStopCallback(nearest.Lat, nearest.Lon,
                    steeringHandle.Route.Stops[steeringHandle.Route.PassedStops].Edge.To);
            }
            else
            {
                Console.WriteLine("No fallback rest area found in external list.");
            }
        }

        /// <summary>
        /// Marks that a rest stop has been planned.
        /// </summary>
        public void MarkPlanned()
        {
            _restAreaPlanned = true;
            _goingToRestArea = true;
        }

        /// <summary>
        /// Cancels rest stop planning.
        /// </summary>
        public void CancelPlanned()
        {
            _goingToRestArea = false;
            _restAreaPlanned = false;
        }

        /// <summary>
        /// Sets the target rest node.
        /// </summary>
        public void SetRestNode(ISpatialNode node)
        {
            _restNode = node;
        }
    }
}
