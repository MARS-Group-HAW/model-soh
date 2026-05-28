using System;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Manages accident-related state and logic for a SemiTruckDriver.
    /// </summary>
    public class AccidentState
    {
        private readonly Random _random;
        private bool _hasAccident;
        private int _accidentTicksRemaining;

        public double DefaultAccidentsPerYear { get; set; }

        public AccidentState()
        {
            _random = new Random();
        }

        /// <summary>
        /// Determines whether a random accident occurs based on accident rate and simulation tick duration.
        /// </summary>
        /// <param name="steeringHandle">The truck's steering handle</param>
        /// <param name="layer">The simulation layer</param>
        /// <param name="truck">The semi truck</param>
        /// <param name="amountOfTrucks">Total number of trucks in simulation</param>
        /// <returns>True if an accident occurred this tick</returns>
        public bool HandleChance(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer, SemiTruck truck, double amountOfTrucks)
        {
            // Adjust accident rate based on total number of trucks in simulation
            double scaledAccidentsPerYear = truck.AccidentsPerYear * (amountOfTrucks / SemiTruckDriverConstants.AccidentTruckScalingBase);
            double ticksPerYear = SemiTruckDriverConstants.SeccondsPerYear / layer._tickDuration.TotalSeconds;
            double accidentChancePerTick = scaledAccidentsPerYear / ticksPerYear;

            // Random draw for accident occurrence
            if (_random.NextDouble() < accidentChancePerTick)
            {
                // Default roadside blocking time (average respond time of ADAC)
                TimeSpan accidentDuration = SemiTruckDriverConstants.DefaultAccidentDuration;

                // Reduce accident duration if shoulder is available
                if (steeringHandle.Route.Count > 0)
                {
                    var currentEdge = steeringHandle.Route[0].Edge;
                    if (currentEdge.Attributes.TryGetValue("shoulder", out var shoulderValue))
                    {
                        var shoulderStr = shoulderValue?.ToString()?.ToLower();
                        if (shoulderStr == "yes" || shoulderStr == "both" || shoulderStr == "left" || shoulderStr == "right")
                        {
                            accidentDuration = SemiTruckDriverConstants.ShoulderAccidentDuration;
                        }
                    }
                }

                _hasAccident = true;
                _accidentTicksRemaining = (int)(accidentDuration.TotalSeconds / layer._tickDuration.TotalSeconds);
                steeringHandle.Stop();

                Console.WriteLine($"Truck {truck.ID} had an accident. Time till Road was unblocked: {accidentDuration.TotalMinutes} minutes.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Continues ticking down the accident time.
        /// Once the accident ends, the truck is removed from the simulation.
        /// </summary>
        /// <param name="driver">The truck driver</param>
        /// <param name="layer">The simulation layer</param>
        /// <param name="removeFromSimulation">Callback to remove truck from simulation</param>
        /// <returns>True if truck is still in accident</returns>
        public bool HandleOngoing(SemiTruckDriver driver, SemiTruckLayer layer, Action removeFromSimulation)
        {
            if (!_hasAccident)
                return false;

            if (_accidentTicksRemaining > 0)
            {
                _accidentTicksRemaining--;
                return true; // Still blocked → skip other logic
            }

            // End accident → remove truck
            removeFromSimulation();
            return true;
        }
    }
}
