using System;
using SOHModel.Database;
using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model.Driver.Utils;
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
        public double FuelCarrierAmount { get; set; }

        private double _drivenKm;
        private double _consumedTotal;

        /// <summary>
        /// Net consumption per 100 km observed so far (including recuperation), or the truck's nominal value
        /// while too little distance has been driven to be meaningful.
        /// </summary>
        public double ObservedConsumptionPer100Km(SemiTruck truck) =>
            _drivenKm >= SemiTruckDriverConstants.MinObservedDistanceKm && _consumedTotal > 0
                ? _consumedTotal / _drivenKm * 100.0
                : truck.FuelConsumptionPer100Km;

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
            double consumedAmount = 0.0;

            // If the route changed or this is the first tick, skip distance calculation to avoid negative values
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
                consumedAmount = truck.FuelConsumptionStrategy.CalculateFuelCarrierAmountUsed(truck, distanceDrivenKm, timeStepSeconds, CurrentIncline);
                FuelCarrierAmount -= consumedAmount;
                _drivenKm += distanceDrivenKm;
                _consumedTotal += consumedAmount;

                // clamp to max energy level
                if (FuelCarrierAmount > truck.MaxFuelCarrierAmount)
                {
                    FuelCarrierAmount = truck.MaxFuelCarrierAmount;
                }

                if (FuelCarrierAmount <= 0)
                {
                    FuelCarrierAmount = 0;
                    // Truck has no energy left; will stop moving until refueled
                    //TODO What should happen when a truck runs out of energy?
                }
            }
            
            LogState(truck, consumedAmount);

            _lastRemainingDistanceToGoal = currentRemainingDistance;
        }

        /// <summary>
        /// Adds energy to the tank during a refuel/recharge stop and logs the new state.
        /// The logged consumed amount is negative (energy added).
        /// </summary>
        public void Charge(SemiTruck truck, double amountAdded)
        {
            FuelCarrierAmount += amountAdded;
            if (FuelCarrierAmount > truck.MaxFuelCarrierAmount - 1e-9) FuelCarrierAmount = truck.MaxFuelCarrierAmount;
            LogState(truck, -amountAdded);
        }

        private void LogState(SemiTruck truck, double consumedAmount)
        {
            PostgresDbLogger.Instance?.Log(new FuelConsumptionEntity(
                truck.ID,
                truck.Layer?.GetCurrentTick() ?? -1,
                truck.FuelConsumptionStrategy.FuelStrategy,
                truck.FuelCarrierType,
                truck.Tank2WheelEfficiency,
                FuelCarrierAmount,
                consumedAmount,
                FuelCarrierAmount / truck.MaxFuelCarrierAmount,
                FuelCarrierEnergyConverter.GetDisplayUnit(truck.FuelCarrierType),
                FuelCarrierEnergyConverter.ToJoules(FuelCarrierAmount, truck.FuelCarrierType),
                FuelCarrierEnergyConverter.ToJoules(consumedAmount, truck.FuelCarrierType)
                )
            );
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
