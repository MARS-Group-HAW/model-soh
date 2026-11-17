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
    /// <param name="MaxReachMeters">If set, the farthest facility within this distance ahead is chosen (as late as possible).</param>
    public record RestRequirement(StopFacility Facility, TimeSpan Duration, bool IsDailyRest, double? MaxReachMeters = null);

    /// <summary>Decides when and where the driver takes the legally required breaks and rests.</summary>
    public interface IRestStrategy
    {
        /// <summary>Returns the stop to plan now, or <c>null</c> if none is due.</summary>
        RestRequirement NextRequirement(DrivingTimeTracker time, SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters, TimeSpan planningMargin);

        /// <summary>
        /// Called when the truck has to charge anyway: returns a rest to take during this charge, or <c>null</c>.
        /// </summary>
        RestRequirement RequirementAtCharge(DrivingTimeTracker time, SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters, TimeSpan planningMargin);

        /// <summary>
        /// The next stop required before the destination, regardless of the planning horizon; <c>null</c> if the
        /// destination is reached within the legal driving time. Used when no later facility is reachable.
        /// </summary>
        RestRequirement RequirementBeforeDestination(DrivingTimeTracker time, SemiTruck truck,
            FuelConsumptionTracker fuel, double remainingRouteMeters);
    }

    /// <summary>Takes every break and rest at a rest area, as late as legally possible.</summary>
    public class LegalMinimumRestStrategy : IRestStrategy
    {
        public RestRequirement NextRequirement(DrivingTimeTracker time, SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters, TimeSpan planningMargin)
        {
            // Search early enough to cover the facility-search horizon, not just a fixed 30-minute margin.
            var searchHorizon = TimeSpan.FromSeconds(SemiTruckDriverConstants.RestAreaSearchRadius / PlanningSpeed.Conservative(truck));
            return Build(time.DueStop(searchHorizon > planningMargin ? searchHorizon : planningMargin),
                time, truck, fuel, remainingRouteMeters);
        }

        public RestRequirement RequirementBeforeDestination(DrivingTimeTracker time, SemiTruck truck,
            FuelConsumptionTracker fuel, double remainingRouteMeters) =>
            Build(time.DueStop(time.LegalDrivingLeft), time, truck, fuel, remainingRouteMeters);

        private RestRequirement Build((TimeSpan Duration, bool IsDailyRest)? due, DrivingTimeTracker time,
            SemiTruck truck, FuelConsumptionTracker fuel, double remainingRouteMeters)
        {
            if (due == null || ArrivesInLegalTime(time, truck, remainingRouteMeters)) return null;

            bool daily = due.Value.IsDailyRest &&
                TimeSpan.FromSeconds(remainingRouteMeters / PlanningSpeed.Conservative(truck)) > time.DailyDrivingLeft;
            var facility = ChooseFacility(truck, fuel, remainingRouteMeters);
            if (!daily && PrefersDailyRestNow(time, truck, remainingRouteMeters, facility)) daily = true;
            // A charging station has to lie within the legal driving time left, otherwise the nearest one found may
            // be reached only after the 4.5 h (or 9 h) limit is exceeded.
            double legalReach = Math.Max(0, time.LegalDrivingLeft.TotalSeconds) * PlanningSpeed.Conservative(truck);
            return new RestRequirement(facility,
                daily ? SemiTruckDriverConstants.DailyRestDuration : time.RemainingBreak, daily, legalReach);
        }

        /// <summary>
        /// A break is due, but a daily rest is needed before the destination as well: taking the daily rest now
        /// instead avoids a break followed shortly by a daily rest, if that does not increase the projected
        /// stationary time (at a charging station equal time is enough, as the longer stop also charges more).
        /// </summary>
        protected static bool PrefersDailyRestNow(DrivingTimeTracker time, SemiTruck truck,
            double remainingRouteMeters, StopFacility facility)
        {
            double drive = remainingRouteMeters / PlanningSpeed.Expected(truck);
            if (TimeSpan.FromSeconds(drive) <= time.DailyDrivingLeft) return false;
            double rest = SemiTruckDriverConstants.DailyRestDuration.TotalSeconds;
            double brk = time.RemainingBreak.TotalSeconds;
            double daily = rest + time.EstimateFurtherRestSeconds(0, rest, drive);
            double separate = brk + time.EstimateFurtherRestSeconds(0, brk, drive);
            return facility == StopFacility.ChargingStation ? daily <= separate : daily < separate;
        }

        public virtual RestRequirement RequirementAtCharge(DrivingTimeTracker time, SemiTruck truck,
            FuelConsumptionTracker fuel, double remainingRouteMeters, TimeSpan planningMargin) => null;

        protected virtual StopFacility ChooseFacility(SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters) => StopFacility.RestArea;

        // No stop is needed if the destination is reached within the legal driving time (conservative speed estimate).
        protected static bool ArrivesInLegalTime(DrivingTimeTracker time, SemiTruck truck, double remainingRouteMeters) =>
            TimeSpan.FromSeconds(remainingRouteMeters / PlanningSpeed.Conservative(truck)) <= time.LegalDrivingLeft;
    }

    /// <summary>
    /// Rests at a charging station whenever the truck cannot reach the destination with its current charge anyway,
    /// so that charging and resting overlap; otherwise it uses a rest area.
    /// </summary>
    public class ChargeWhileRestingStrategy : LegalMinimumRestStrategy
    {
        /// <summary>
        /// Merges a due rest, or a daily rest needed before the destination when merging it does not increase the
        /// projected stationary time.
        /// </summary>
        public override RestRequirement RequirementAtCharge(DrivingTimeTracker time, SemiTruck truck,
            FuelConsumptionTracker fuel, double remainingRouteMeters, TimeSpan planningMargin)
        {
            if (truck.FuelCarrierType != FuelCarrierType.Battery) return null;
            var due = NextRequirement(time, truck, fuel, remainingRouteMeters, planningMargin);
            if (due != null) return due;

            // Take a daily rest needed before the destination during this charge if that does not add stationary
            // time: the charge (at least a break long) would otherwise be followed by a separate daily rest later.
            if (!PrefersDailyRestNow(time, truck, remainingRouteMeters, StopFacility.ChargingStation)) return null;
            double legalReach = Math.Max(0, time.LegalDrivingLeft.TotalSeconds) * PlanningSpeed.Conservative(truck);
            return new RestRequirement(StopFacility.ChargingStation, SemiTruckDriverConstants.DailyRestDuration, true,
                legalReach);
        }

        protected override StopFacility ChooseFacility(SemiTruck truck, FuelConsumptionTracker fuel,
            double remainingRouteMeters)
        {
            if (truck.FuelCarrierType != FuelCarrierType.Battery) return StopFacility.RestArea;

            // A charge is unavoidable before the destination: taking it during the rest hides the charging time.
            double range = fuel.PlanningRangeKm(truck);
            bool needsCharge = remainingRouteMeters > (range - SemiTruckDriverConstants.RouteChargeReserveKm) * 1000;
            return needsCharge ? StopFacility.ChargingStation : StopFacility.RestArea;
        }
    }
}
