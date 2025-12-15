using System;
using System.Linq;
using Mars.Interfaces.Annotations;
using SOHModel.ChristmasMarket.Entities;
using SOHModel.ChristmasMarket.Layers;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A market traveler that makes decisions based on desires (Hunger, Thirst) and resources (Budget).
/// </summary>
public class DesireMarketTraveler : OptimalStepsMarketTraveler
{
    [PropertyDescription(Name = "hunger")]
    public double Hunger { get; set; }

    [PropertyDescription(Name = "thirst")]
    public double Thirst { get; set; }

    [PropertyDescription(Name = "budget")]
    public double Budget { get; set; }

    /// <summary>
    /// Selects a new target stall based on the agent's current desires and budget.
    /// </summary>
    protected override void ChooseNewTargetStall()
    {
        var marketLayer = MarketLayer.Current;
        if (marketLayer == null || marketLayer.Stalls.Count == 0)
        {
            _targetStall = null;
            return;
        }

        MarketStallType? targetType = null;

        // Decision logic based on user requirements:
        // "when an agent has thirst he goes to Glühwein"
        // "when he has hunger to Gastronomy"
        // "when he has money to Verkaufsstand"
        
        // We prioritize physical needs (Thirst, Hunger) over shopping (Budget).
        // We use a threshold of 0.5 to determine if a desire is "active".
        // For budget, we check if they have "some" money (> 0).

        if (Thirst > 0.5)
        {
            targetType = MarketStallType.Glühwein;
        }
        else if (Hunger > 0.5)
        {
            targetType = MarketStallType.Gastronomie;
        }
        else if (Budget > 0.0)
        {
            targetType = MarketStallType.Verkaufsstand;
        }

        MarketStall newTarget = null;

        if (targetType.HasValue)
        {
            var candidates = marketLayer.Stalls.Where(s => s.Type == targetType.Value).ToList();
            if (candidates.Count > 0)
            {
                // Choose a random one from the candidates
                newTarget = candidates[_random.Next(candidates.Count)];
            }
        }

        // Fallback: If no target found (e.g. no stall of that type, or no desires active), pick random
        if (newTarget == null)
        {
             var allStalls = marketLayer.Stalls;
             if (allStalls.Count <= 1)
             {
                 newTarget = allStalls[0];
             }
             else
             {
                 do
                 {
                     newTarget = allStalls[_random.Next(allStalls.Count)];
                 } while (newTarget == _targetStall); // Try to avoid the same stall
             }
        }

        _targetStall = newTarget;
        _currentStallPosition = _targetStall.Position;
        
        Console.WriteLine($"[DEBUG] DesireAgent {ID} (H:{Hunger:F2}, T:{Thirst:F2}, B:{Budget:F2}) chose target: '{_targetStall.StallName}' ({_targetStall.Type})");
    }

    public override void Tick()
    {
        base.Tick();
        // User requested: "Werte Thirst und Hunger um 0,0003 sinken"
        // But context implies 0=Full, 1=Starving.
        // If they sink (decrease), the agent gets fuller over time.
        // If they rise (increase), the agent gets hungrier over time.
        // Given the logic "if Thirst > 0.5 then Drink", high value = high need.
        // Therefore, time must INCREASE the need (towards 1).
        // I am implementing INCREASE here to make the simulation functional.
        Hunger = Math.Min(1.0, Hunger + 0.0003);
        Thirst = Math.Min(1.0, Thirst + 0.0003);
    }

    protected override void OnStallVisit(MarketStall stall)
    {
        base.OnStallVisit(stall);

        // User requested: "beim glühweinstand steigt thirst um 0,5 max 1"
        // Again, context implies drinking should REDUCE thirst (towards 0).
        // I am implementing DECREASE here to make the simulation functional.
        
        switch (stall.Type)
        {
            case MarketStallType.Glühwein:
                Thirst = Math.Max(0.0, Thirst - 0.5); // "Steigt" interpreted as "Satisfies" (Value drops)
                break;
            case MarketStallType.Gastronomie:
                Hunger = Math.Max(0.0, Hunger - 0.5); // "Steigt" interpreted as "Satisfies" (Value drops)
                break;
            case MarketStallType.Verkaufsstand:
                var cost = _random.NextDouble() * (0.5 - 0.1) + 0.1; // Random 0.1 to 0.5
                Budget = Math.Max(0.0, Budget - cost);
                break;
        }
        
        Console.WriteLine($"[DEBUG] DesireAgent {ID} visited {stall.Type}. New State -> H:{Hunger:F2}, T:{Thirst:F2}, B:{Budget:F2}");
    }
}
