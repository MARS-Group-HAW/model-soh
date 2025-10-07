using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace SOHModel.ChristmasMarket;

public class MarketStall : IEntity
{
    public Guid ID { get; set; }
    public Position Position { get; set; }
    
    [PropertyDescription(Name = "type")] 
    public MarketStallType Type { get; set; }
    
    [PropertyDescription(Name = "name")]
    public string StallName { get; set; }

    public void Init(Position position, MarketStallType type, string stallName)
    {
        ID = Guid.NewGuid();
        Position = position;
        Type = type;
        StallName = stallName;
    }
}