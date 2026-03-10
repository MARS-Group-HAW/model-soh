namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Naive implementation that calculates fuel consumption linearly based on distance.
    /// </summary>
    public class LinearFuelConsumptionStrategy : IFuelConsumptionStrategy
    {
        public FuelStrategyType FuelStrategy => FuelStrategyType.Linear;

        public double CalculateEnergyCarrierAmountUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
        {
            return (truck.FuelConsumptionPer100Km / 100.0) * distanceDrivenKm;
        }

        public double EstimateRemainingRangeKm(SemiTruck truck, double currentEnergyLevel)
        {
            if (truck.FuelConsumptionPer100Km <= 0) return double.PositiveInfinity;
            return (currentEnergyLevel / truck.FuelConsumptionPer100Km) * 100.0;
        }
    }
}
