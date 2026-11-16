using System;
using SOHModel.Database;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Implementation of fuel consumption based on the road load equation.
    /// F_total = F_rolling + F_drag + F_gradient + F_acceleration
    /// Yields the energy at the wheels only; losses and recuperation depend on the energy store that supplies it.
    /// Not 100% accurate:
    /// - Brake = discard energy (stores that can recuperate take part of it back)
    /// - Zero Speed = Zero fuel (idling costs energy, also)
    /// </summary>
    public class RoadLoadFuelConsumptionStrategy : IFuelConsumptionStrategy
    {
        public FuelStrategyType FuelStrategy => FuelStrategyType.RoadLoad;

        private const double AirDensity = 1.225; // kg/m^3
        private const double Gravity = 9.81; // m/s^2
        

        public double CalculateWheelEnergyJoules(SemiTruck truck, double distanceDrivenKm, double timeStepSeconds, double incline)
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
            double energyJoules = powerWatts * timeStepSeconds;

            PostgresDbLogger.Instance?.Log(new RoadLoadEntity(truck.ID, truck.Layer?.GetCurrentTick() ?? 0, v, a, m, A,
                incline, efficiency, F_rolling, F_drag, F_gradient, F_accel, F_total, powerWatts, energyJoules));
            
            return energyJoules;
        }
    }
}
