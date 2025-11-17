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
    }
}