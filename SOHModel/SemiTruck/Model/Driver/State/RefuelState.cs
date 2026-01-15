using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages refueling related state and logic for a SemiTruckDriver.
    /// </summary>
    public class RefuelState : StopState
    {
        private bool _isRefueling;

        protected override RestStateType GetStopType() => RestStateType.Refuel;

        protected override TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck)
        {
            return TimeSpan.FromMinutes(truck.RefuelTimeInMinutes);
        }

        protected override string[] GetSearchTags() => new[] { "services", "fuel", "charging_station" };

        protected override IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer) => layer.AllRefuelStations;

        protected override double GetSearchRadius() => SemiTruckDriverConstants.RefuelStationSearchRadius;

        protected override bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            // Estimate how far the truck can go with current energy (in km)
            double availableRangeKm = truck.FuelConsumptionStrategy.EstimateRemainingRangeKm(truck, fuelTracker.EnergyLevel);

            // Check condition: If range is too low and there's distance to go
            if (availableRangeKm < SemiTruckDriverConstants.LowFuelThreshold)
            {
                // Only refuel if there's still a long distance to go
                if (steeringHandle.Route.RemainingRouteDistanceToGoal > SemiTruckDriverConstants.RefuelStationSearchRadius)
                {
                    Console.WriteLine($"[Truck {truck.ID}] Energy low: {availableRangeKm:F1} km remaining – searching for refuel station...");
                    return true;
                }
            }
            return false;
        }

        protected override void OnPauseCompleted(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            fuelTracker.EnergyLevel = truck.MaxEnergyAmount; // Reset to full
            _isRefueling = false;
        }

        protected override void OnArrival(SemiTruckLayer layer)
        {
            _isRefueling = true;
        }

        protected override bool IsPauseCompleted() => _isRefueling;

        protected override void MarkPauseCompleted(bool completed)
        {
            _isRefueling = completed;
        }

        /// <summary>
        /// Sets the target refuel node.
        /// Wrapper for compatibility with existing code.
        /// </summary>
        public void SetRefuelNode(ISpatialNode node)
        {
            SetTargetNode(node);
        }
    }
}
