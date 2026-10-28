using System;
using SOHModel.Domain.Model;

namespace SOHModel.SemiTruck.Model.Driver.Utils
{
    /// <summary>
    /// Power model for refueling/recharging. The transfer power is limited by the weaker side (station or vehicle)
    /// and, for batteries, tapers off at high state of charge (constant-current/constant-voltage approximation).
    /// Power is expressed as kW-equivalent for every carrier (energy per time), so diesel and hydrogen
    /// flow rates are given by their energy content. Charging losses are not modelled.
    /// </summary>
    public static class ChargingProfile
    {
        /// <summary>SoC (0-1) up to which a battery is charged with full power.</summary>
        private const double TaperStartSoc = 0.8;

        /// <summary>Fraction of the full power that is still available at 100% SoC.</summary>
        private const double TaperEndPowerFraction = 0.1;

        /// <summary>Default power a station delivers if no station-specific value is known.</summary>
        private static double DefaultStationPowerKw(FuelCarrierType type) => type switch
        {
            FuelCarrierType.Battery => 400,
            FuelCarrierType.Fuel => 30_000, // ~50 L/min diesel
            FuelCarrierType.Hydrogen => 6_000, // ~3 kg/min at 350 bar
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        /// <summary>Default power a vehicle accepts if the truck type does not define <c>maxChargePowerKw</c>.</summary>
        private static double DefaultVehiclePowerKw(FuelCarrierType type) => type switch
        {
            FuelCarrierType.Battery => 350,
            FuelCarrierType.Fuel => 30_000,
            FuelCarrierType.Hydrogen => 6_000,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        /// <summary>Default SoC (0-1) at which a battery stops charging; the CV tail above is slow, so trucks leave early.</summary>
        private const double DefaultBatteryTargetSoc = 0.8;

        /// <summary>
        /// Amount (L, kWh, kg) at which a stop ends: <c>chargeTargetSoc</c> of the capacity if set (0 &lt; value &lt;= 1),
        /// otherwise 80% for batteries and 100% for fuel and hydrogen. An override takes precedence over both.
        /// </summary>
        public static double TargetAmount(SemiTruck truck, double? targetSocOverride = null)
        {
            if (targetSocOverride is > 0 and <= 1) return truck.MaxFuelCarrierAmount * targetSocOverride.Value;
            double soc = truck.ChargeTargetSoc is > 0 and <= 1 ? truck.ChargeTargetSoc
                : truck.FuelCarrierType == FuelCarrierType.Battery ? DefaultBatteryTargetSoc : 1.0;
            return truck.MaxFuelCarrierAmount * soc;
        }

        /// <summary>Relative power (0-1) at the given state of charge; only batteries taper.</summary>
        public static double TaperFactor(FuelCarrierType type, double soc)
        {
            if (type != FuelCarrierType.Battery || soc <= TaperStartSoc) return 1.0;
            double t = Math.Min(1.0, (soc - TaperStartSoc) / (1.0 - TaperStartSoc));
            return 1.0 - t * (1.0 - TaperEndPowerFraction);
        }

        /// <summary>
        /// Power in kW-equivalent at the given SoC (0-1): min(station, vehicle) scaled by the taper factor.
        /// </summary>
        public static double PowerKw(SemiTruck truck, double soc, double? stationPowerKw = null)
        {
            double station = stationPowerKw is > 0 ? stationPowerKw.Value : DefaultStationPowerKw(truck.FuelCarrierType);
            double vehicle = truck.MaxChargePowerKw > 0 ? truck.MaxChargePowerKw : DefaultVehiclePowerKw(truck.FuelCarrierType);
            return Math.Min(station, vehicle) * TaperFactor(truck.FuelCarrierType, soc);
        }

        /// <summary>
        /// Amount (in the carrier's unit: L, kWh, kg) transferred during one time step, never exceeding the amount missing to the target.
        /// </summary>
        public static double AmountAdded(SemiTruck truck, double currentAmount, double timeStepSeconds,
            double? stationPowerKw = null, double? targetSocOverride = null)
        {
            double missing = TargetAmount(truck, targetSocOverride) - currentAmount;
            if (missing <= 0 || timeStepSeconds <= 0) return 0;

            double soc = truck.MaxFuelCarrierAmount > 0 ? currentAmount / truck.MaxFuelCarrierAmount : 1.0;
            double joules = PowerKw(truck, soc, stationPowerKw) * 1000.0 * timeStepSeconds;
            return Math.Min(missing, FuelCarrierEnergyConverter.FromJoules(joules, truck.FuelCarrierType));
        }
    }
}
