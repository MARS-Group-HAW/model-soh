using System;
using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>Selectable rest planning strategy of a truck type (CSV column <c>restStrategy</c>).</summary>
    public enum RestStrategyType
    {
        /// <summary>Breaks and daily rests at rest areas, legal minimum only.</summary>
        LegalMinimum,

        /// <summary>Like <see cref="LegalMinimum"/>, but rests at a charging station if the truck has to charge anyway.</summary>
        ChargeWhileResting
    }

    public enum StopFacility { RestArea, ChargingStation }

    /// <summary>A stop the driver has to take: where, and for at least how long.</summary>
    public record RestRequirement(StopFacility Facility, TimeSpan Duration, bool IsDailyRest);

    /// <summary>Decides when and where the driver takes the legally required breaks and rests.</summary>
    public interface IRestStrategy
    {
        /// <summary>Returns the stop to plan now, or <c>null</c> if none is due.</summary>
        RestRequirement NextRequirement(DrivingTimeTracker time, SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters, TimeSpan planningMargin);
    }

    /// <summary>Takes every break and rest at a rest area, as late as legally possible.</summary>
    public class LegalMinimumRestStrategy : IRestStrategy
    {
        public RestRequirement NextRequirement(DrivingTimeTracker time, SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters, TimeSpan planningMargin)
        {
            var due = time.DueStop(planningMargin);
            if (due == null || ArrivesInLegalTime(time, truck, remainingRouteMeters)) return null;

            return new RestRequirement(ChooseFacility(truck, fuel, remainingRouteMeters), due.Value.Duration,
                due.Value.IsDailyRest);
        }

        protected virtual StopFacility ChooseFacility(SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters) => StopFacility.RestArea;

        // No stop is needed if the destination is reached within the legal driving time (conservative speed estimate).
        private static bool ArrivesInLegalTime(DrivingTimeTracker time, SemiTruck truck, double remainingRouteMeters)
        {
            double speed = Math.Max(1.0, truck.MaxSpeed * SemiTruckDriverConstants.PlanningSpeedFraction);
            return TimeSpan.FromSeconds(remainingRouteMeters / speed) <= time.LegalDrivingLeft;
        }
    }

    /// <summary>
    /// Rests at a charging station whenever the truck cannot reach the destination with its current charge anyway,
    /// so that charging and resting overlap; otherwise it uses a rest area.
    /// </summary>
    public class ChargeWhileRestingStrategy : LegalMinimumRestStrategy
    {
        protected override StopFacility ChooseFacility(SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters)
        {
            if (truck.FuelCarrierType != FuelCarrierType.Battery) return StopFacility.RestArea;

            // Same criterion the charging state applies: charge only if the energy for the rest of the route is missing.
            var targetSoc = RefuelState.RouteBasedTargetSoc(truck, fuel, remainingRouteMeters);
            bool needsCharge = targetSoc.HasValue
                ? fuel.FuelCarrierAmount < targetSoc.Value * truck.MaxFuelCarrierAmount
                : fuel.PlanningRangeKm(truck) < remainingRouteMeters / 1000.0 + SemiTruckDriverConstants.LowFuelThreshold;
            return needsCharge ? StopFacility.ChargingStation : StopFacility.RestArea;
        }
    }
}
