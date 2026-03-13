using System;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Entities;
using SOHModel.ChristmasMarket.Layers;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A market traveler that makes decisions based on desires (Hunger, Thirst) and resources (Budget).
/// </summary>
public class DesireMarketTraveler : OptimalStepsMarketTraveler
{
    [PropertyDescription(Name = "hunger")]
    public double hunger { get; set; }

    [PropertyDescription(Name = "thirst")]
    public double thirst { get; set; }

    [PropertyDescription(Name = "budget")]
    public double budget { get; set; }

    [PropertyDescription(Name = "bladder")]
    public double bladder { get; set; }

    [PropertyDescription(Name = "bac")]
    public double bac { get; set; }

    [PropertyDescription(Name = "exhaustion")]
    public double exhaustion { get; set; }

    [PropertyDescription(Name = "mood")]
    public double mood { get; set; }

    /// <summary>
    /// Initializes the agent with random starting values.
    /// </summary>
    /// <param name="layer">The layer the agent is on.</param>
    public override void Init(MarketTravelerLayer layer)
    {
        base.Init(layer);
        
        // Randomize initial states so not everyone is identical
        mood = _random.NextDouble() * 0.1 + 0.5; // 0.1 to 1.0 (Happy-ish)
        budget = _random.NextDouble() * 80.0 + 20.0; // 20 to 100 Euro
        
        bladder = _random.NextDouble() * 0.5; // 0.0 to 0.5
        hunger = _random.NextDouble() * 0.3;
        thirst = _random.NextDouble() * 0.4;
        exhaustion = 0.0; // Start fresh
        bac = 0.0; // Start sober
    }

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

        if (bladder > 0.8)
        {
            targetType = MarketStallType.Toilette;
        }
        else if (thirst > 0.5)
        {
            targetType = MarketStallType.Glühwein;
        }
        else if (hunger > 0.5)
        {
            targetType = MarketStallType.Gastronomie;
        }
        else if (exhaustion > 0.7)
        {
            targetType = MarketStallType.Feuertonne;
        }
        else if (mood < 0.4)
        {
            targetType = MarketStallType.Bühne;
        }
        else if (budget < 5.0)
        {
            targetType = MarketStallType.Geldautomat;
        }
        else if (budget > 0.0)
        {
            targetType = MarketStallType.Verkaufsstand;
        }

        MarketStall newTarget = null;

        if (targetType.HasValue)
        {
            var candidates = marketLayer.Stalls.Where(s => s.Type == targetType.Value).ToList();
            if (candidates.Count > 0)
            {
                // Choose the one with the best "score" (distance + queue penalty)
                // Score = Distance + (QueueLength * 5.0) -> 5 meters equivalent per person in line
                newTarget = candidates
                    .OrderBy(s => Position.DistanceInMTo(s.Position) + (s.WaitingQueue.Count * 5.0))
                    .First();
            }
        }

        // Fallback: If no target found, pick random
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
                 } while (newTarget == _targetStall); 
             }
        }

        _targetStall = newTarget;
        _currentStallPosition = _targetStall.Position;
    }

    /// <summary>
    /// Overrides the arrival behavior to use queues instead of immediate visiting.
    /// </summary>
    protected override void HandleArrivedAtStall(MarketStall stall)
    {
        // Try to enter queue
        if (stall.EnterQueue(ID))
        {
            _state = VisitorState.WaitingForService;
        }
        else
        {
            // If full or error, maybe wait a bit then retry or pick new?
            // For now, let's just stay in OnMarket and retry next tick
        }
    }

    public override void Tick()
    {
        // Intercept Tick for queue states
        if (_state == VisitorState.WaitingForService || _state == VisitorState.BeingServed)
        {
            HandleServiceInteraction();
            
            // Still run simulation logic for needs, but skipping movement
            SimulateDesires();
            return;
        }

        // Normal behavior
        base.Tick();
        SimulateDesires();
    }

    private void SimulateDesires()
    {
        // Given the logic "if Thirst > 0.5 then Drink", high value = high need.
        // Therefore, time must INCREASE the need (towards 1).
        hunger = Math.Min(1.0, hunger + 0.0003);
        thirst = Math.Min(1.0, thirst + 0.0003);
        
        // Bladder increases with time, stronger than hunger/thirst usually if drinking
        bladder = Math.Min(1.0, bladder + 0.0005);
        
        // Exhaustion increases with time (standing, walking, cold)
        exhaustion = Math.Min(1.0, exhaustion + 0.0002);
        
        // BAC decreases with time (sobering up)
        bac = Math.Max(0.0, bac - 0.0001);

        // Mood might decay slightly over time if needs are high? Or stay stable.
        // Let's say Mood decays if Exhaustion is high or Bladder is high.
        // Mood decays slightly over time (boredom), faster if needs are high
        var moodDecay = 0.00005; // Base decay
        
        if (exhaustion > 0.6 || bladder > 0.6)
        {
            moodDecay += 0.0002; // Faster decay when uncomfortable
        }
        
        // Waiting in line is boring/annoying!
        if (_state == VisitorState.WaitingForService)
        {
             moodDecay += 0.0001;
        }

        mood = Math.Max(0.0, mood - moodDecay);

        // Mood influences leaveprobability
        // Base is 0.01
        if (mood < 0.3)
        {
            leaveprobability = 0.05; // High chance to leave if unhappy
        }
        else if (mood > 0.8)
        {
            leaveprobability = 0.001; // Low chance to leave if happy
        }
        else
        {
            leaveprobability = 0.01; // Normal
        }
    }
    
    /// <summary>
    /// Handles the logic when the agent is waiting in line or being served.
    /// </summary>
    private void HandleServiceInteraction()
    {
        if (_targetStall == null)
        {
             // Something went wrong, stall disappeared? Back to market mode.
             _state = VisitorState.OnMarket;
             return;
        }

        if (_state == VisitorState.WaitingForService)
        {
            // Check if we are now being served
            if (_targetStall.IsServing(ID))
            {
                _state = VisitorState.BeingServed;
            }
        }
        
        if (_state == VisitorState.BeingServed)
        {
            // Check if service is finished
            if (_targetStall.IsFinished(ID))
            {
                // Done!
                OnServiceFinished(_targetStall);
                
                // Reset for next action
                _targetStall = null;
                _state = VisitorState.OnMarket;
            }
        }
    }

    // REMOVED OnStallVisit override because satisfaction now happens at END of service

    protected override void OnServiceFinished(MarketStall stall)
    {
        base.OnServiceFinished(stall);
        
        // Apply effects ONLY after being served
        switch (stall.Type)
        {
            case MarketStallType.Glühwein:
                thirst = Math.Max(0.0, thirst - 0.5); 
                bladder = Math.Min(1.0, bladder + 0.1); 
                bac = bac + 0.05; 
                mood = Math.Min(1.0, mood + 0.1); 
                break;
            case MarketStallType.Gastronomie:
                hunger = Math.Max(0.0, hunger - 0.5); 
                break;
            case MarketStallType.Verkaufsstand:
                var cost = _random.NextDouble() * (10.0 - 3.0) + 3.0;
                budget = Math.Max(0.0, budget - cost);
                break;
            case MarketStallType.Toilette:
                bladder = 0.0;
                break;
            case MarketStallType.Feuertonne:
                exhaustion = Math.Max(0.0, exhaustion - 0.2); 
                break;
            case MarketStallType.Geldautomat:
                budget += 50.0; 
                break;
            case MarketStallType.Bühne:
                mood = Math.Min(1.0, mood + 0.3); 
                break;
        }
    }
}
