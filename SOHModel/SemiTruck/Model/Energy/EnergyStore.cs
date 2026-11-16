using SOHModel.Domain.Model;

namespace SOHModel.SemiTruck.Model.Energy
{
    /// <summary>
    /// One energy storage of a truck (diesel tank, battery, hydrogen tank). It knows how wheel energy translates
    /// into its own unit (L, kWh, kg), so that callers never have to branch on the carrier type.
    /// </summary>
    public sealed class EnergyStore
    {
        /// <summary>Share of the braking energy that ends up in a battery (see <see cref="CanRecuperate"/>).</summary>
        public const double RecuperationFactor = 0.9;

        private const double MinEfficiency = 0.01;

        public EnergyStore(FuelCarrierType type, double capacity, double tankToWheelEfficiency, double initialAmount)
        {
            Type = type;
            Capacity = capacity;
            Efficiency = Math.Max(MinEfficiency, tankToWheelEfficiency);
            Amount = initialAmount;
        }

        public FuelCarrierType Type { get; }

        /// <summary>Maximum amount in the carrier's unit (L, kWh, kg).</summary>
        public double Capacity { get; }

        /// <summary>Tank-to-wheel efficiency (0-1) of the path from this store to the wheels.</summary>
        public double Efficiency { get; }

        /// <summary>Current amount in the carrier's unit (L, kWh, kg).</summary>
        public double Amount { get; set; }

        public double StateOfCharge => Capacity > 0 ? Amount / Capacity : 0;

        /// <summary>Only batteries take braking energy back.</summary>
        public bool CanRecuperate => Type == FuelCarrierType.Battery;

        /// <summary>Energy at the wheels that the current content can deliver.</summary>
        public double WheelJoulesAvailable => WheelJoules(Type, Amount, Efficiency);

        /// <summary>Energy at the wheels that the content would deliver at the given state of charge.</summary>
        public double WheelJoulesAtStateOfCharge(double soc) => WheelJoules(Type, Capacity * soc, Efficiency);

        public static double WheelJoules(FuelCarrierType type, double amount, double tankToWheelEfficiency) =>
            FuelCarrierEnergyConverter.ToJoules(amount, type) * Math.Max(MinEfficiency, tankToWheelEfficiency);

        /// <summary>
        /// Amount (carrier unit) this store gives up to provide the given wheel energy; negative if wheel energy is
        /// negative (braking) and recuperated into the store.
        /// </summary>
        public double AmountFor(double wheelJoules)
        {
            if (wheelJoules >= 0) return FuelCarrierEnergyConverter.FromJoules(wheelJoules / Efficiency, Type);
            return CanRecuperate
                ? FuelCarrierEnergyConverter.FromJoules(wheelJoules * Efficiency * RecuperationFactor, Type)
                : 0;
        }

        /// <summary>
        /// Provides (positive) or recuperates (negative) wheel energy, keeping the content within 0 and the capacity.
        /// </summary>
        /// <returns>The amount demanded from the store (negative = added), also if the store ran empty meanwhile.</returns>
        public double Draw(double wheelJoules)
        {
            double used = AmountFor(wheelJoules);
            Amount -= used;
            if (Amount > Capacity) Amount = Capacity;
            if (Amount <= 0) Amount = 0;
            return used;
        }

        /// <summary>Adds energy during a refuel/recharge stop, up to the capacity.</summary>
        public void Add(double amount)
        {
            Amount += amount;
            if (Amount > Capacity - 1e-9) Amount = Capacity;
        }
    }
}
