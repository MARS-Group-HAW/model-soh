using SOHModel.SemiTruck.Model;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Specifies the type of fuel consumption strategy used for a SemiTruck.
    /// </summary>
    public enum FuelStrategyType
    {
        Linear,
        RoadLoad
    }
    
    /// <summary>
    /// Defines a strategy for calculating fuel/energy consumption for a SemiTruck.
    /// </summary>
    public interface IFuelConsumptionStrategy
    {
        abstract FuelStrategyType FuelStrategy { get; } 
        /// <summary>
        /// Calculates the energy the truck needs at its wheels during the last tick. Losses and recuperation
        /// are handled by the truck's <see cref="Energy.EnergyStore"/>.
        /// </summary>
        /// <param name="truck">The SemiTruck instance.</param>
        /// <param name="distanceDrivenKm">The distance driven during the last tick in km.</param>
        /// <param name="timeStepSeconds">The duration of the last tick in seconds.</param>
        /// <param name="incline">The current incline of the road in percent.</param>
        /// <returns>Wheel energy in joules; negative if the truck brakes and energy is available for recuperation.</returns>
        double CalculateWheelEnergyJoules(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline);

    }

    public static class FuelConsumptionStrategyExtensions
    {
        /// <summary>
        /// Amount of the truck's fuel carrier (see <see cref="SOHModel.Domain.Model.RoadUser.FuelCarrierType"/>)
        /// used to supply the wheel energy of the last tick.
        /// </summary>
        public static double CalculateFuelCarrierAmountUsed(this IFuelConsumptionStrategy strategy, SemiTruck truck,
            double distanceDrivenKm, double timeStepSeconds, double incline) =>
            new Energy.EnergyStore(truck.FuelCarrierType, truck.MaxFuelCarrierAmount, truck.Tank2WheelEfficiency, 0)
                .AmountFor(strategy.CalculateWheelEnergyJoules(truck, distanceDrivenKm, timeStepSeconds, incline));
    }
}
