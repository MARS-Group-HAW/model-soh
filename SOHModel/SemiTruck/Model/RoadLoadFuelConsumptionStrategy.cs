using System;
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
        
        public double CalculateEnergyUsed(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
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
            
            // Engines usually don't recover energy when braking (unless electric, handled simply here)
            if (F_total < 0) F_total = 0;

            // Power = Force * Velocity
            double powerWatts = F_total * v;
            
            // Energy = Power * Time (scaled by efficiency)
            double energyJoules = (powerWatts / Math.Max(0.01, truck.Efficiency)) * timeStepSeconds;

            // Convert Joules to Energy units
            return energyJoules / GetJoulesPerUnit(truck.EnergyType);
        }

        public double EstimateRemainingRangeKm(SemiTruck truck, double currentEnergyLevel)
        {
            // Fallback to linear estimation using EnergyConsumptionPer100Km for range prediction
            if (truck.EnergyConsumptionPer100Km <= 0) return double.PositiveInfinity;
            return (currentEnergyLevel / truck.EnergyConsumptionPer100Km) * 100.0;
        }

        // TODO combine with Well-to-Wheel (WTW) and Tank-to-Wheel (TTW) efficiencies for better comparison https://doi.org/10.1016/j.enconman.2022.115412
        private static double GetJoulesPerUnit(EnergyType type)
        {
            return type switch
            {
                EnergyType.Fuel => 36_000_000,     // 36 MJ/L (Diesel)
                EnergyType.Battery => 3_600_000,    // 3.6 MJ/kWh
                EnergyType.Hydrogen => 120_000_000, // 120 MJ/kg
                _ => 36_000_000
            };
        }
    }
}
