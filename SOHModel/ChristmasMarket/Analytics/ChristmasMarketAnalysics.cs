using SOHModel.ChristmasMarket.Entities;

namespace SOHModel.ChristmasMarket.Analytics;

/// <summary>
/// A static class for collecting simulation data for the Christmas market.
/// It captures metrics such as the number of visitors per stall and the duration of agents' stays.
/// </summary>
public static class ChristmasMarketAnalysics
    {
        private static readonly Dictionary<string, int> _stallVisitorCounts = new Dictionary<string, int>();

        private static readonly List<long> _agentMarketDurations = new List<long>();
        
        private static readonly Dictionary<Guid, long> _agentEntryTicks = new Dictionary<Guid, long>();

        /// <summary>
        /// Records an agent's visit to a specific market stall.
        /// Increments the visitor count for the visited stall.
        /// </summary>
        /// <param name="stall">The market stall that was visited.</param>
        public static void RecordStallVisit(MarketStall stall)
        {
            if (stall == null || string.IsNullOrEmpty(stall.StallName)) return;

            // Zählt den Besuch für den jeweiligen Stand
            if (!_stallVisitorCounts.ContainsKey(stall.StallName))
            {
                _stallVisitorCounts[stall.StallName] = 0;
            }
            _stallVisitorCounts[stall.StallName]++;
        }

        /// <summary>
        /// Records the simulation tick at which an agent enters the market.
        /// </summary>
        /// <param name="agentId">The unique ID of the agent.</param>
        /// <param name="currentTick">The current simulation tick of the entry event.</param>
        public static void RecordAgentEntry(Guid agentId, long currentTick)
        {
            _agentEntryTicks[agentId] = currentTick;
        }

        /// <summary>
        /// Records an agent's exit from the market and calculates their total duration of stay.
        /// </summary>
        /// <param name="agentId">The unique ID of the agent leaving the market.</param>
        /// <param name="currentTick">The current simulation tick of the exit event.</param>
        public static void RecordAgentExit(Guid agentId, long currentTick)
        {
            if (_agentEntryTicks.TryGetValue(agentId, out var entryTick))
            {
                var duration = currentTick - entryTick;
                _agentMarketDurations.Add(duration);
                _agentEntryTicks.Remove(agentId);
            }
        }

        /// <summary>
        /// Prints a formatted summary of the collected simulation results to the console.
        /// </summary>
        public static void PrintResults()
        {
            Console.WriteLine("\n--- Simulationsanalyse ---");

            Console.WriteLine("\nBesucher pro Stand:");
            if (_stallVisitorCounts.Count == 0)
            {
                Console.WriteLine("Keine Standbesuche aufgezeichnet.");
            }
            else
            {
                var sortedStalls = _stallVisitorCounts.OrderByDescending(kv => kv.Value);
                foreach (var entry in sortedStalls)
                {
                    Console.WriteLine($"- {entry.Key}: {entry.Value} Besuche");
                }
            }

            Console.WriteLine("\nVerweildauer der Agenten auf dem Markt:");
            if (_agentMarketDurations.Count == 0)
            {
                Console.WriteLine("Keine Daten zur Verweildauer aufgezeichnet.");
            }
            else
            {
                var averageDurationSeconds = _agentMarketDurations.Average();
                var minDuration = _agentMarketDurations.Min();
                var maxDuration = _agentMarketDurations.Max();
                Console.WriteLine($"  - Durchschnittliche Verweildauer: {averageDurationSeconds:F2} Sekunden");
                Console.WriteLine($"  - Kürzeste Verweildauer: {minDuration} Sekunden");
                Console.WriteLine($"  - Längste Verweildauer: {maxDuration} Sekunden");
            }
            Console.WriteLine("\n--------------------------\n");
        }
    }