using System;
using SOHModel.Database;
using SOHModel.Domain.Model;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Implementation of fuel consumption based on the road load equation.
    /// F_total = F_rolling + F_drag + F_gradient + F_acceleration
    /// Not 100% accurate:
    /// - Brake = discard energy (EVs can recharge)
    /// - Zero Speed = Zero fuel (idling costs energy, also)
    /// </summary>
    public class RoadLoadFuelConsumptionStrategy : IFuelConsumptionStrategy
    {
        public FuelStrategyType FuelStrategy => FuelStrategyType.RoadLoad;

        private const double AirDensity = 1.225; // kg/m^3
        private const double Gravity = 9.81; // m/s^2
        
        // recuperation blending (alpha) changes can change with a bunch of factors:
        // in many cases, like downhill with mild braking and moderate SoC (State of Charge), alpha is around 1.
        // a full battery: alpha << 1. very low speed: alpha -> 0.
        // right now, we assume alpha = 0.9 for simplicity.
        private const double RecuperationFactor = 0.9; 
        
        public double CalculateFuelCarrierAmountUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
        {
            if (distanceDrivenKm <= 0 || timeStepSeconds <= 0) return 0;

            double v = truck.Velocity; // m/s
            double a = truck.Acceleration; // m/s^2
            double m = truck.Mass; // kg
            double A = truck.Width * truck.Height; // Frontal area (m^2)
            
            if (A <= 0) A = 6.25; // Default for 2.5x2.5 truck

            // 1. Rolling Resistance: F_rolling = Crr * m * g * cos(theta)
            // For small angles, cos(theta) ~ 1
            double F_rolling = truck.RollingResistance * m * Gravity;

            // 2. Aerodynamic Drag: F_drag = 0.5 * rho * Cd * A * v^2
            double F_drag = 0.5 * AirDensity * truck.DragCoefficient * A * v * v;

            // 3. Gradient Force: F_gradient = m * g * sin(theta)
            // incline is in percent, so slope = incline / 100
            // theta = arctan(slope), sin(theta) = slope / sqrt(1 + slope^2)
            double slope = incline / 100.0;
            double F_gradient = m * Gravity * (slope / Math.Sqrt(1 + slope * slope));

            // 4. Acceleration Force: F_accel = m * a
            double F_accel = m * a;

            // Total force
            double F_total = F_rolling + F_drag + F_gradient + F_accel;

            // Power = Force * Velocity
            double powerWatts = F_total * v;

            double efficiency = Math.Max(0.01, truck.Tank2WheelEfficiency);

            double energyJoules = CalculateEnergyTransfer(truck, powerWatts, efficiency, timeStepSeconds);

            PostgresDbLogger.Instance.Log(new RoadLoadEntity(truck.ID, truck.Layer.GetCurrentTick(), v, a, m, A,
                incline, efficiency, F_rolling, F_drag, F_gradient, F_accel, F_total, powerWatts, energyJoules));
            
            // Convert Joules to Energy units
            return FuelCarrierEnergyConverter.FromJoules(energyJoules, truck.FuelCarrierType);
        }

        /// <summary>
        /// calculates the energy transfer from a power source to a fuel carrier, or the other way around (recuperation).
        /// </summary>
        /// <returns>energy which transferred from power source to fuel carrier (consumption) or the other way around (recuperation)</returns>
        private static double CalculateEnergyTransfer(SemiTruck truck, double powerWatts, double tank2WheelEfficiency,
            double timeStepSeconds)
        {
            double energyJoules;

            if (powerWatts >= 0)
            {
                // propulsion: stored energy -> wheel energy
                energyJoules = (powerWatts / tank2WheelEfficiency) * timeStepSeconds;
            }
            else if (truck.FuelCarrierType == FuelCarrierType.Battery)
            {
                // recuperation for battery-electric trucks:
                // wheel/braking energy -> stored battery energy
                // negative result means battery charge is increased.
                energyJoules = powerWatts * tank2WheelEfficiency * RecuperationFactor * timeStepSeconds;
            }
            else
            {
                // non-battery trucks do not recuperate
                energyJoules = 0;
            }

            return energyJoules;
        }

        // TODO instead of linear estimation, use more accurate model
        // some options:
        // 1. steady state baseline: assume a=0 and theta=0 to get F_steady
        // 2. exponential moving average: use historical consumption of last few kilometers to project future
        // 3. horizon prediction: since we know the whole route (not accounting for stops) we can project segments of the whole route
        // see https://doi.org/10.1504/IJVD.2024.10062758 for example
        public double EstimateRemainingRangeKm(SemiTruck truck, double currentFuelCarrierAmount)
        {
            // Fallback to linear estimation using EnergyConsumptionPer100Km for range prediction
            if (truck.FuelConsumptionPer100Km <= 0) return double.PositiveInfinity;
            return (currentFuelCarrierAmount / truck.FuelConsumptionPer100Km) * 100.0;
        }
    }
}
