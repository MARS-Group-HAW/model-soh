using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace SOHModel.ChristmasMarket.Entities;

/// <summary>
/// Represents a single stall at the Christmas market.
/// This entity stores all relevant information such as the stall's position, type, and name.
/// </summary>
public class MarketStall : IEntity
{
    public Guid ID { get; set; }
    public Position Position { get; set; }
    
    [PropertyDescription(Name = "type")] 
    public MarketStallType Type { get; set; }
    
    [PropertyDescription(Name = "name")]
    public string StallName { get; set; }
    
    // -- Queue & Capacity Logic --

    public int Capacity { get; set; } = 1;
    public int ServiceTime { get; set; } = 1; // Ticks needed to serve
    
    // Agents waiting to be served
    public Queue<Guid> WaitingQueue { get; } = new();
    
    // Agents currently being served -> Value is remaining service ticks
    public Dictionary<Guid, int> ServingAgents { get; } = new();

    /// <summary>
    /// Initializes an instance of a market stall with the provided values.
    /// </summary>
    /// <param name="position">The position of the stall.</param>
    /// <param name="type">The type of the stall.</param>
    /// <param name="stallName">The name of the stall.</param>
    public void Init(Position position, MarketStallType type, string stallName)
    {
        ID = Guid.NewGuid();
        Position = position;
        Type = type;
        StallName = stallName;
        
        // Defaults, overridden by MarketLayer Init logic potentially
        Capacity = 1; 
        ServiceTime = 10;
    }

    /// <summary>
    /// Enqueues an agent for service.
    /// </summary>
    public bool EnterQueue(Guid agentId)
    {
        if (WaitingQueue.Contains(agentId) || ServingAgents.ContainsKey(agentId)) return false;
        
        WaitingQueue.Enqueue(agentId);
        return true;
    }

    /// <summary>
    /// Checks if the agent is currently being served.
    /// </summary>
    public bool IsServing(Guid agentId)
    {
        return ServingAgents.ContainsKey(agentId);
    }

    /// <summary>
    /// Checks if the agent has finished service.
    /// If yes, removes from Serving list.
    /// </summary>
    public bool IsFinished(Guid agentId)
    {
        // For Stage (Infinite capacity/service), we might handle differently, 
        // but generally this checks if RemainingTime <= 0
        if (ServingAgents.ContainsKey(agentId) && ServingAgents[agentId] <= 0)
        {
            ServingAgents.Remove(agentId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes agent from queue or serving list (e.g. if they leave due to impatience).
    /// </summary>
    public void Leave(Guid agentId)
    {
        if (ServingAgents.ContainsKey(agentId))
        {
            ServingAgents.Remove(agentId);
        }
        // Removing from Queue is hard with generic Queue<T>, but we can ignore for now or rebuild queue.
        // For simulation simplicity, we assume once queued, they stay or we implement custom removal later.
    }

    /// <summary>
    /// Processes the queue and service times.
    /// </summary>
    public void Tick()
    {
        // 1. Move from Queue to Serving if capacity allows
        // For Stage, Capacity should be set very high (e.g. int.MaxValue)
        while (WaitingQueue.Count > 0 && ServingAgents.Count < Capacity)
        {
            var agentId = WaitingQueue.Dequeue();
            // Start serving
            // If Stage, service time might be irrelevant (0), or fixed duration they want to watch.
            // Here we assume ServiceTime is the mandatorystaff interaction time.
            ServingAgents[agentId] = ServiceTime; 
        }

        // 2. Decrement remaining service time
        foreach (var agentId in ServingAgents.Keys.ToList())
        {
            ServingAgents[agentId]--;
        }
    }
}