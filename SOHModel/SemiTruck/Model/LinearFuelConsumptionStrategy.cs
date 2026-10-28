namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Naive implementation that calculates fuel consumption linearly based on distance.
    /// </summary>
    public class LinearFuelConsumptionStrategy : IFuelConsumptionStrategy
    {
        public FuelStrategyType FuelStrategy => FuelStrategyType.Linear;

        public double CalculateFuelCarrierAmountUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
        {
            return (truck.FuelConsumptionPer100Km / 100.0) * distanceDrivenKm;
        }
    }
}
