using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model.Energy;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Tracks and manages fuel/energy consumption for a truck. It draws the wheel energy from the truck's
    /// <see cref="EnergyStore"/>, observes the consumption and derives range estimates from it.
    /// </summary>
    public class FuelConsumptionTracker
    {
        private double _lastRemainingDistanceToGoal = -1;
        private bool _routeChanged;

        public double CurrentIncline { get; private set; }

        private double _drivenKm;
        private double _consumedWheelJoules;

        /// <summary>Creates the tracker with a full energy store according to the truck's fuel carrier.</summary>
        public FuelConsumptionTracker(SemiTruck truck)
        {
            Store = new EnergyStore(truck.FuelCarrierType, truck.MaxFuelCarrierAmount, truck.Tank2WheelEfficiency,
                truck.MaxFuelCarrierAmount);
        }

        public EnergyStore Store { get; }

        /// <summary>Amount in the energy store, in its unit (L, kWh, kg).</summary>
        public double FuelCarrierAmount
        {
            get => Store.Amount;
            set => Store.Amount = value;
        }

        /// <summary>Consumption per 100 km (carrier unit) to plan with: the larger of the nominal and the observed value.</summary>
        public double PlanningConsumptionPer100Km(SemiTruck truck)
        {
            return FuelCarrierEnergyConverter.FromJoules(PlanningWheelJoulesPerKm(truck) / Store.Efficiency, Store.Type) * 100.0;
        }

        /// <summary>
        /// Wheel energy per km to plan with: the larger of the nominal and the observed value. Nominal is the truck's
        /// <c>fuelConsumptionPer100km</c>, observed is the trip average so far.
        /// </summary>
        // TODO instead of the trip-average observed consumption, use a more accurate model
        // some options:
        // 1. steady state baseline: assume a=0 and theta=0 to get F_steady
        // 2. exponential moving average: use historical consumption of last few kilometers to project future
        // 3. horizon prediction: since we know the whole route (not accounting for stops) we can project segments of the whole route
        // see https://doi.org/10.1504/IJVD.2024.10062758 for example
        public double PlanningWheelJoulesPerKm(SemiTruck truck) =>
            Math.Max(NominalWheelJoulesPerKm(truck), ObservedWheelJoulesPerKm(truck));

        /// <summary>Range in km with the current energy at the planning consumption.</summary>
        public double PlanningRangeKm(SemiTruck truck)
        {
            double perKm = PlanningWheelJoulesPerKm(truck);
            return perKm > 0 ? Store.WheelJoulesAvailable / perKm : double.PositiveInfinity;
        }

        /// <summary>Amount (carrier unit) expected to be left after driving <paramref name="meters"/>.</summary>
        public double EstimatedAmountAfter(double meters, SemiTruck truck) =>
            Math.Max(0, FuelCarrierAmount - meters / 100_000 * PlanningConsumptionPer100Km(truck));

        /// <summary>
        /// True if the energy on board covers the edges: their distance lies within the planning range, and the
        /// wheel energy suffices up to every point on the way (see <see cref="RequiredWheelJoules"/>).
        /// </summary>
        public bool CanCover(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge, SemiTruck truck)
        {
            var (meters, joules) = Requirement(edges, positionOnFirstEdge, truck);
            return meters <= PlanningRangeKm(truck) * 1000 && joules <= Store.WheelJoulesAvailable;
        }

        /// <summary>
        /// Wheel energy needed to drive the edges: the planning consumption per distance plus the potential energy of
        /// the elevation gained (descents are credited only partially). Taken as the maximum over every elevation
        /// sample, so the truck must also be able to reach the highest point on the way.
        /// </summary>
        internal double RequiredWheelJoules(IEnumerable<ISpatialEdge> edges, double positionOnFirstEdge,
            SemiTruck truck) => Requirement(edges, positionOnFirstEdge, truck).WheelJoules;

        private (double Meters, double WheelJoules) Requirement(IEnumerable<ISpatialEdge> edges,
            double positionOnFirstEdge, SemiTruck truck)
        {
            double perMeter = PlanningWheelJoulesPerKm(truck) / 1000.0;
            double weight = Math.Max(0, truck.Mass) * 9.81;
            double along = 0;
            double potential = 0;
            double worst = 0;
            bool first = true;
            foreach (var edge in edges)
            {
                double start = first ? Math.Clamp(positionOnFirstEdge, 0, edge.Length) : 0;
                first = false;
                var profile = ElevationProfile.Of(edge);
                if (profile == null || edge.Length <= 0)
                {
                    along += edge.Length - start;
                    worst = Math.Max(worst, perMeter * along + potential);
                    continue;
                }

                double step = edge.Length / (profile.Length - 1);
                double previousX = start;
                double previousH = ElevationProfile.At(profile, edge.Length, start);
                for (int i = (int)Math.Floor(start / step) + 1; i < profile.Length; i++)
                {
                    double x = i * step;
                    double rise = profile[i] - previousH;
                    along += x - previousX;
                    potential += weight * (rise >= 0 ? rise : rise * SemiTruckDriverConstants.DescentRecoveryFactor);
                    worst = Math.Max(worst, perMeter * along + potential);
                    previousX = x;
                    previousH = profile[i];
                }
            }
            return (along, worst);
        }

        /// <summary>
        /// Net consumption per 100 km (carrier unit) observed so far (including recuperation), or the truck's
        /// nominal value while too little distance has been driven to be meaningful.
        /// </summary>
        public double ObservedConsumptionPer100Km(SemiTruck truck)
        {
            return FuelCarrierEnergyConverter.FromJoules(ObservedWheelJoulesPerKm(truck) / Store.Efficiency, Store.Type) * 100.0;
        }

        private double NominalWheelJoulesPerKm(SemiTruck truck)
        {
            return EnergyStore.WheelJoules(Store.Type, truck.FuelConsumptionPer100Km / 100.0, Store.Efficiency);
        }

        private double ObservedWheelJoulesPerKm(SemiTruck truck) =>
            _drivenKm >= SemiTruckDriverConstants.MinObservedDistanceKm && _consumedWheelJoules > 0
                ? _consumedWheelJoules / _drivenKm
                : NominalWheelJoulesPerKm(truck);

        /// <summary>
        /// Marks that the route has changed, which affects fuel consumption tracking.
        /// </summary>
        public void MarkRouteChanged()
        {
            _routeChanged = true;
        }

        public void BeginMovement(SemiTruckSteeringHandle steeringHandle)
        {
            _routeChanged = false;
            _lastRemainingDistanceToGoal = steeringHandle.Route.RemainingRouteDistanceToGoal;
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

                // Calculate the energy needed at the wheels and withdraw it from the store
                double timeStepSeconds = layer._tickDuration.TotalSeconds;
                double wheelJoules = truck.FuelConsumptionStrategy.CalculateWheelEnergyJoules(truck, distanceDrivenKm, timeStepSeconds, CurrentIncline);
                consumedAmount = Store.Draw(wheelJoules);
                _drivenKm += distanceDrivenKm;
                _consumedWheelJoules += EnergyStore.WheelJoules(Store.Type, consumedAmount, Store.Efficiency);

            }

            LogState(truck, consumedAmount);

            _lastRemainingDistanceToGoal = currentRemainingDistance;
        }

        /// <summary>
        /// Adds energy to the store during a refuel/recharge stop and logs the new state.
        /// The logged consumed amount is negative (energy added).
        /// </summary>
        public void Charge(SemiTruck truck, double amountAdded)
        {
            Store.Add(amountAdded);
            LogState(truck, -amountAdded);
        }

        private void LogState(SemiTruck truck, double consumedAmount)
        {
            PostgresDbLogger.Instance?.Log(new FuelConsumptionEntity(
                truck.ID,
                truck.Layer?.GetCurrentTick() ?? -1,
                truck.FuelConsumptionStrategy.FuelStrategy,
                Store.Type,
                Store.Efficiency,
                Store.Amount,
                consumedAmount,
                Store.Amount / Store.Capacity,
                FuelCarrierEnergyConverter.GetDisplayUnit(Store.Type),
                FuelCarrierEnergyConverter.ToJoules(Store.Amount, Store.Type),
                FuelCarrierEnergyConverter.ToJoules(consumedAmount, Store.Type)
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
