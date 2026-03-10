using System;
using SOHModel.Database;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Tracks and manages fuel/energy consumption for a truck.
    /// </summary>
    public class FuelConsumptionTracker
    {
        private double _lastRemainingDistanceToGoal = -1;
        private bool _routeChanged;

        public double CurrentIncline { get; private set; }
        public double EnergyCarrierAmount { get; set; }

        /// <summary>
        /// Marks that the route has changed, which affects fuel consumption tracking.
        /// </summary>
        public void MarkRouteChanged()
        {
            _routeChanged = true;
        }

        /// <summary>
        /// Calculates and updates the fuel consumption based on the distance driven since the last tick.
        /// </summary>
        /// <param name="steeringHandle">The truck's steering handle</param>
        /// <param name="layer">The simulation layer</param>
        /// <param name="truck">The semi truck</param>
        public void UpdateConsumption(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer, SemiTruck truck)
        {
            double currentRemainingDistance = steeringHandle.Route.RemainingRouteDistanceToGoal;
            double distanceDrivenKm = 0.0;

            // If the route changed, skip distance calculation to avoid negative values
            if (_routeChanged || _lastRemainingDistanceToGoal < 0)
            {
                _routeChanged = false;
            }
            else
            {
                // Calculate how far the truck moved (in km)
                distanceDrivenKm = (_lastRemainingDistanceToGoal - currentRemainingDistance) / 1000.0;
                if (distanceDrivenKm < 0) distanceDrivenKm = 0;

                // Calculate energy usage and reduce level
                double timeStepSeconds = layer._tickDuration.TotalSeconds;
                double consumedAmount = truck.FuelConsumptionStrategy.CalculateEnergyCarrierAmountUsed(truck, distanceDrivenKm, timeStepSeconds, CurrentIncline);
                EnergyCarrierAmount -= consumedAmount;

                if (EnergyCarrierAmount <= 0)
                {
                    EnergyCarrierAmount = 0;
                    // Truck has no energy left; will stop moving until refueled
                    //TODO What should happen when a truck runs out of energy?
                }
                
                PostgresDbLogger.Instance?.Log(new FuelConsumptionEntity(truck.ID, truck.Layer?.GetCurrentTick() ?? -1, truck.FuelConsumptionStrategy.FuelStrategy, truck.FuelCarrierType, truck.Tank2WheelEfficiency, EnergyCarrierAmount, consumedAmount));
            }

            _lastRemainingDistanceToGoal = currentRemainingDistance;
        }

        /// <summary>
        /// Updates the current incline value.
        /// </summary>
        public void UpdateIncline(double incline)
        {
            CurrentIncline = incline;
        }
    }
}
