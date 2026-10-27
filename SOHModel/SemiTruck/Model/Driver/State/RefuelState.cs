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
        private bool _targetReached;

        /// <summary>
        /// Power in kW-equivalent of the station the truck is currently heading to / standing at.
        /// <c>null</c> uses the default station power of the truck's fuel carrier type.
        /// </summary>
        public double? StationPowerKw { get; set; }

        protected override RestStateType GetStopType() => RestStateType.Refuel;

        // The duration emerges from the charging process, there is no fixed pause time.
        protected override TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck) => TimeSpan.Zero;

        protected override string[] GetSearchTags() => new[] { "services", "fuel", "charging_station" };

        protected override IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer) => layer.AllRefuelStations;

        protected override double GetSearchRadius() => SemiTruckDriverConstants.RefuelStationSearchRadius;

        protected override bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            // Estimate how far the truck can go with current energy (in km)
            double availableRangeKm = truck.FuelConsumptionStrategy.EstimateRemainingRangeKm(truck, fuelTracker.FuelCarrierAmount);

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
            _targetReached = false;
        }

        protected override void OnArrival(SemiTruckLayer layer)
        {
            _targetReached = false;
        }

        /// <summary>
        /// Transfers energy for one tick (power-limited, tapering for batteries) and logs the new fuel level.
        /// </summary>
        protected override void OnPauseTick(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            double added = ChargingProfile.AmountAdded(truck, fuelTracker.FuelCarrierAmount,
                layer._tickDuration.TotalSeconds, StationPowerKw);
            fuelTracker.Charge(truck, added);
            _targetReached = fuelTracker.FuelCarrierAmount >= truck.MaxFuelCarrierAmount;
        }

        protected override bool IsPauseCompleted(SemiTruckLayer layer, SemiTruck truck) => _targetReached;
    }
}
