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
        /// Calculates the amount of fuel used during the last tick.
        /// </summary>
        /// <param name="truck">The SemiTruck instance.</param>
        /// <param name="distanceDrivenKm">The distance driven during the last tick in km.</param>
        /// <param name="timeStepSeconds">The duration of the last tick in seconds.</param>
        /// <param name="incline">The current incline of the road in percent.</param>
        /// <returns>The amount of fuel used (in the same unit as the <see cref="SOHModel.Domain.Model.RoadUser.FuelCarrierType"/>).</returns>
        double CalculateFuelCarrierAmountUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline);

    }
}
