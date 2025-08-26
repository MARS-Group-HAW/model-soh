using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Domain.Model;

public class MarketStall : IEntity
{
    public Guid ID { get; set; }
    public Position Position { get; set; }
    public MarketStallType Type { get; set; }
    public string Name { get; set; }

    public void Init(Position position, MarketStallType type, string name)
    {
        ID = Guid.NewGuid();
        Position = position;
        Type = type;
        Name = name;
    }
    
}