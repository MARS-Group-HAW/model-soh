using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;
using SOHModel.SemiTruck.Model.Driver.Services;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages refueling related state and logic for a SemiTruckDriver.
    /// </summary>
    public class RefuelState : StopState
    {
        private bool _targetReached;
        private RestRequirement? _combinedRest;

        /// <summary>
        /// Power in kW-equivalent of the station the truck is currently heading to / standing at.
        /// <c>null</c> uses the default station power of the truck's fuel carrier type.
        /// </summary>
        public double? StationPowerKw { get; set; }

        /// <summary>
        /// Requests a stop at a charging station that also serves as the driver's break or daily rest: the stop lasts
        /// at least the required rest time and uses the normal route-based or configured charge target.
        /// </summary>
        public void RequestCombinedRest(RestRequirement requirement) => _combinedRest = requirement;

        public bool HasCombinedRestRequest => _combinedRest != null;

        public RestRequirement? TakeRestAreaFallback()
        {
            if (!PlanningFailed || IsActive || _combinedRest == null) return null;
            var rest = _combinedRest with { Facility = StopFacility.RestArea };
            _combinedRest = null;
            // After a legal rest, previously time-limited charging approaches can become reachable again.
            ResetFacilitySearch();
            return rest;
        }

        protected override RestStateType GetStopType() =>
            _combinedRest != null ? RestStateType.RestAndRefuel : RestStateType.Refuel;

        // The duration emerges from the charging process, there is no fixed pause time.
        protected override TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck) =>
            _combinedRest?.Duration ?? TimeSpan.Zero;

        // The map only knows fuel stations (tag "fuel"), which cannot charge a battery: those trucks rely on
        // "charging_station" edges and otherwise on the charging station list.
        protected override string[] GetSearchTags() => _carrierType == FuelCarrierType.Battery
            ? new[] { "charging_station" }
            : new[] { "services", "fuel" };

        // Battery trucks use the charging station list, fuel and hydrogen trucks the fuel station list.
        protected override IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer) =>
            _carrierType == FuelCarrierType.Battery ? layer.AllChargingStations : layer.AllRefuelStations;

        protected override void OnFacilityChosen(dynamic facility)
        {
            StationPowerKw = facility?.PowerKw;
        }

        private FuelCarrierType _carrierType;

        protected override double GetSearchRadius() => SemiTruckDriverConstants.RefuelStationSearchRadius;

        protected override (double Seconds, double Charge) EstimateStopCost(StopRoutePlan plan,
            SemiTruckLayer layer, SemiTruck truck, FuelConsumptionTracker fuel, object? facility)
        {
            var station = facility as SemiTruckLayer.RefuelStation;
            station ??= GetFacilityList(layer).OfType<SemiTruckLayer.RefuelStation>()
                .FirstOrDefault(s => GeometryHelper.ApproxDistanceMeters(s.Lat, s.Lon,
                    plan.StopNode.Position.Latitude, plan.StopNode.Position.Longitude) < 100);
            double? power = station?.PowerKw;
            double arrivalAmount = fuel.EstimatedAmountAfter(plan.Approach.Meters, truck);
            double? target = RouteBasedTargetSoc(truck, fuel, plan.Onward.Meters);
            double seconds = ChargingProfile.SecondsToTarget(truck, arrivalAmount, power, target);
            return (Math.Max(seconds, _combinedRest?.Duration.TotalSeconds ?? 0),
                Math.Max(0, ChargingProfile.TargetAmount(truck, target) - arrivalAmount));
        }

        protected override double ArrivalReserveMeters() => SemiTruckDriverConstants.RouteChargeReserveKm * 1000;

        protected override double? GetReachLimitMeters(SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            double reach = Math.Max(fuelTracker.PlanningRangeKm(truck) * 1000, 0);
            return _combinedRest?.MaxReachMeters is { } legalReach
                ? Math.Min(reach, legalReach)
                : reach;
        }

        protected override bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            _carrierType = truck.FuelCarrierType;
            return _combinedRest != null || IsChargeDue(steeringHandle, truck, fuelTracker);
        }

        /// <summary>True if the energy left calls for a stop (ignoring legal rests).</summary>
        public bool IsChargeDue(SemiTruckSteeringHandle steeringHandle, SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            _carrierType = truck.FuelCarrierType;

            // The station may be up to the search radius ahead, so the stop must be triggered while the truck can still
            // reach it: observed (not nominal) consumption, a station-distance allowance and an arrival reserve.
            double availableRangeKm = fuelTracker.PlanningRangeKm(truck);
            double triggerRangeKm = SemiTruckDriverConstants.RefuelStationSearchRadius / 1000.0
                                    + SemiTruckDriverConstants.RouteChargeReserveKm;

            return availableRangeKm < triggerRangeKm &&
                   steeringHandle.Route.RemainingRouteDistanceToGoal > availableRangeKm * 1000.0;
        }

        protected override void OnPauseCompleted(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            _targetReached = false;
            _combinedRest = null;
            _chargeTargetOverride = null;
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
            if (_targetReached) return;
            // While a combined rest is still running, charging beyond the default target costs no time.
            bool restRunning = _combinedRest != null &&
                               layer._simulationTime < _stopStartTime + _combinedRest.Duration;
            _chargeTargetOverride = RouteBasedTargetSoc(truck, fuelTracker, driver.RemainingRouteDistanceToGoal) ??
                (restRunning && truck.FuelCarrierType == FuelCarrierType.Battery && truck.ChargeTargetSoc <= 0
                    ? 1.0 : null);
            double added = ChargingProfile.AmountAdded(truck, fuelTracker.FuelCarrierAmount,
                layer._tickDuration.TotalSeconds, StationPowerKw, _chargeTargetOverride);
            fuelTracker.Charge(truck, added);
            _targetReached = fuelTracker.FuelCarrierAmount >= ChargingProfile.TargetAmount(truck, _chargeTargetOverride) - 1e-9;
        }

        /// <summary>
        /// For battery trucks without a fixed <c>chargeTargetSoc</c>: the SoC that just covers the remaining route
        /// (observed consumption, safety factor and arrival reserve). Charging beyond what the route needs only wastes
        /// time in the slow CV phase; if the route needs more than a full battery, the default target applies and
        /// another stop follows. <c>null</c> = use the default target.
        /// </summary>
        internal static double? RouteBasedTargetSoc(SemiTruck truck, FuelConsumptionTracker fuelTracker, double remainingRouteMeters)
        {
            if (truck.FuelCarrierType != FuelCarrierType.Battery || truck.ChargeTargetSoc > 0 ||
                truck.MaxFuelCarrierAmount <= 0) return null;

            double perKm = fuelTracker.PlanningConsumptionPer100Km(truck) / 100.0;
            double neededKwh = perKm * (remainingRouteMeters / 1000.0 * SemiTruckDriverConstants.RouteChargeSafetyFactor
                                        + SemiTruckDriverConstants.RouteChargeReserveKm);
            double soc = neededKwh / truck.MaxFuelCarrierAmount;
            return soc is > 0 and <= 1.0 ? soc : null;
        }

        private double? _chargeTargetOverride;

        protected override bool IsPauseCompleted(SemiTruckLayer layer, SemiTruck truck) =>
            _targetReached && (_combinedRest == null ||
                               layer._simulationTime >= _stopStartTime + _combinedRest.Duration);
    }
}
