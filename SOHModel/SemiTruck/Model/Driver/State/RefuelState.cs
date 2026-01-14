using System;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages refueling related state and logic for a SemiTruckDriver.
    /// </summary>
    public class RefuelState
    {
        private DateTime _refuelUntilTime = DateTime.MinValue;
        private bool _refuelPlanned;
        private bool _goingToRefuel;
        private bool _isRefueling;
        private ISpatialNode _refuelNode;

        /// <summary>
        /// Handles the pause logic when the truck is refueling.
        /// </summary>
        /// <returns>True if refueling is ongoing and tick should pause</returns>
        public bool HandlePause(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver semiTruckDriver)
        {
            // Truck is currently in refueling phase
            if (_isRefueling && _refuelUntilTime > layer._simulationTime)
                return true;

            // Refueling just completed
            if (_isRefueling && _refuelUntilTime <= layer._simulationTime)
            {
                Console.WriteLine($"[Truck {truck.ID}] Refueling/Recharging completed – continuing trip.");
                PostgresDbLogger.Instance.Log(new RestEntity(semiTruckDriver.ID,
                    layer.Context.CurrentTick,
                    RestStateType.Refuel,
                    RestEventType.End,
                    semiTruckDriver.Position.Longitude,
                    semiTruckDriver.Position.Latitude,
                    semiTruckDriver.EnergyLevel
                ));
                fuelTracker.EnergyLevel = truck.EnergyAmount; // Reset to full
                _isRefueling = false;
                _refuelPlanned = false;
                _refuelNode = null;
            }

            // Truck has arrived at the fueling point
            if (_goingToRefuel &&
                steeringHandle.Position != null &&
                _refuelNode != null &&
                GeometryHelper.IsOnNode(steeringHandle.Position, _refuelNode))
            {
                Console.WriteLine($"[Truck {truck.ID}] Refuel station reached. Starting pause ({truck.RefuelTimeInMinutes} min).");
                PostgresDbLogger.Instance.Log(new RestEntity(semiTruckDriver.ID,
                    layer.Context.CurrentTick,
                    RestStateType.Refuel,
                    RestEventType.Start,
                    semiTruckDriver.Position.Longitude,
                    semiTruckDriver.Position.Latitude,
                    semiTruckDriver.EnergyLevel
                ));
                _refuelUntilTime = layer._simulationTime + TimeSpan.FromMinutes(truck.RefuelTimeInMinutes);
                _isRefueling = true;
                _goingToRefuel = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether refueling is required and plans route to station if needed.
        /// </summary>
        public void CheckAndPlanRefuel(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, Action<double, double, ISpatialNode> planStopCallback)
        {
            // Skip if already on a refueling mission
            if (_goingToRefuel || _refuelPlanned) return;

            // Estimate how far the truck can go with current energy (in km)
            double availableRangeKm = truck.FuelConsumptionStrategy.EstimateRemainingRangeKm(truck, fuelTracker.EnergyLevel);

            // If range is too low, prepare refuel plan
            if (availableRangeKm < SemiTruckDriverConstants.LowFuelThreshold)
            {
                // Only search if there's still a long distance to go
                if (steeringHandle.Route.RemainingRouteDistanceToGoal > SemiTruckDriverConstants.RefuelStationSearchRadius)
                {
                    double accumulatedDistance = 0;
                    Console.WriteLine($"[Truck {truck.ID}] Energy low: {availableRangeKm:F1} km remaining – searching for refuel station...");

                    // Scan the next 100 km along the route for gas station tags
                    foreach (var routeStep in steeringHandle.Route)
                    {
                        var edge = routeStep.Edge;
                        accumulatedDistance += edge.Length;
                        if (accumulatedDistance > SemiTruckDriverConstants.RefuelStationSearchRadius)
                            break;

                        var nodesToCheck = new[] { edge.From, edge.To };

                        foreach (ISpatialNode node in nodesToCheck)
                        {
                            foreach (var connectedEdge in node.OutgoingEdges.Values)
                            {
                                if (connectedEdge.Attributes.TryGetValue("source_tag", out var tag))
                                {
                                    var tagStr = tag?.ToString()?.ToLowerInvariant();
                                    if (tagStr == "services" || tagStr == "fuel" || tagStr == "charging_station")
                                    {
                                        var serviceCoord = connectedEdge.To.Position;
                                        var insertFromNode = connectedEdge.From;

                                        planStopCallback(serviceCoord.Latitude, serviceCoord.Longitude, insertFromNode);
                                        _refuelPlanned = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    // No refuel station found on the way – fallback to external list
                    Console.WriteLine($"[Truck {truck.ID}] No refuel station found along route – fallback to CSV list.");
                    FindNearestFromList(steeringHandle, layer, planStopCallback);
                    _refuelPlanned = true;
                }
            }
        }

        /// <summary>
        /// Finds nearest refuel station from external CSV list.
        /// </summary>
        private void FindNearestFromList(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            Action<double, double, ISpatialNode> planStopCallback)
        {
            var currentLat = steeringHandle.Position.Latitude;
            var currentLon = steeringHandle.Position.Longitude;

            var nearest = layer.AllRefuelStations.MinBy(r =>
                GeometryHelper.GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));

            if (nearest != null)
            {
                Console.WriteLine($"Using fallback refuel station from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                planStopCallback(nearest.Lat, nearest.Lon,
                    steeringHandle.Route.Stops[steeringHandle.Route.PassedStops].Edge.To);
            }
            else
            {
                Console.WriteLine("No fallback refuel station found in external list.");
            }
        }

        /// <summary>
        /// Marks that a refuel stop has been planned.
        /// </summary>
        public void MarkPlanned()
        {
            _refuelPlanned = true;
            _goingToRefuel = true;
        }

        /// <summary>
        /// Cancels refuel stop planning.
        /// </summary>
        public void CancelPlanned()
        {
            _goingToRefuel = false;
            _refuelPlanned = false;
        }

        /// <summary>
        /// Sets the target refuel node.
        /// </summary>
        public void SetRefuelNode(ISpatialNode node)
        {
            _refuelNode = node;
        }
    }
}
