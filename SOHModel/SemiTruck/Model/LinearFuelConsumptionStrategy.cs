namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Naive implementation that calculates fuel consumption linearly based on distance.
    /// </summary>
    public class LinearFuelConsumptionStrategy : IFuelConsumptionStrategy
    {
        public FuelStrategyType FuelStrategy => FuelStrategyType.Linear;

        public double CalculateWheelEnergyJoules(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
        {
            double amount = (truck.FuelConsumptionPer100Km / 100.0) * distanceDrivenKm;
            return Energy.EnergyStore.WheelJoules(truck.FuelCarrierType, amount, truck.Tank2WheelEfficiency);
        }
    }
}
