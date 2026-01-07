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
        /// <summary>
        /// Calculates the amount of energy used during the last tick.
        /// </summary>
        /// <param name="truck">The SemiTruck instance.</param>
        /// <param name="distanceDrivenKm">The distance driven during the last tick in km.</param>
        /// <param name="timeStepSeconds">The duration of the last tick in seconds.</param>
        /// <param name="incline">The current incline of the road in percent.</param>
        /// <returns>The amount of energy used (in the same unit as EnergyLevel).</returns>
        double CalculateEnergyUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline);

        /// <summary>
        /// Estimates the remaining range of the truck in km based on the current energy level.
        /// </summary>
        /// <param name="truck">The SemiTruck instance.</param>
        /// <param name="currentEnergyLevel">The current energy level.</param>
        /// <returns>The estimated remaining range in km.</returns>
        double EstimateRemainingRangeKm(SemiTruck truck, double currentEnergyLevel);
    }
}
