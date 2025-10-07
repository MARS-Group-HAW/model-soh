using System.Collections.Concurrent;
using Mars.Components.Layers;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.ChristmasMarket;
using SOHModel.Multimodal.Model;

namespace SOHModel.Domain.Model;

public class MarketLayer : VectorLayer, ILayer
{
    private long _currentTick;
    public static MarketLayer Current { get; private set; }
    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;
    private readonly HashSet<MarketTraveler> _activeTravelers = new();
    private static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();

    public ISimulationContext Context { get; private set; }

    private readonly List<MarketStall> _stalls = new();
    public IReadOnlyList<MarketStall> Stalls => _stalls;
    
    [PropertyDescription(Name = "topLeftCorner")]
    public string TopLeftCorner { get; set; }

    [PropertyDescription(Name = "topRightCorner")]
    public string TopRightCorner { get; set; }

    [PropertyDescription(Name = "bottomRightCorner")]
    public string BottomRightCorner { get; set; }

    [PropertyDescription(Name = "bottomLeftCorner")]
    public string BottomLeftCorner { get; set; }

    private List<(double lon, double lat)> _marketPolygon;
    private bool _polygonParsed;


    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle, UnregisterAgent unregisterAgentHandle)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);
        
        Context = layerInitData?.Context;
        Current = this;

        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;
        
        foreach (var feature in Features)
        {
            var stall = new MarketStall();

            var properties = feature.VectorStructured.Data;
            if (feature.VectorStructured?.Geometry != null)
            {
                var centroid = feature.VectorStructured.Geometry.Centroid;
                stall.Position = new Position(centroid.X, centroid.Y);
            }

            if (properties.TryGetValue("name", out var nameObject) && nameObject is string stallName)
            {
                stall.StallName = stallName;
            }

            if (properties.TryGetValue("type", out var typeObject))
            {
                int typeAsInt = Convert.ToInt32(typeObject);
                stall.Type = (MarketStallType)typeAsInt;
            }
            
            stall.ID = Guid.NewGuid();
            _stalls.Add(stall);
        }

        Console.WriteLine($"[INFO] Loaded and initialized {_stalls.Count} market stalls via EntityManager");
        foreach (var stall in _stalls)
        {
            Console.WriteLine($"[INFO] -> Stall '{stall.StallName}' of type '{stall.Type}' at ({stall.Position.X:F6}, {stall.Position.Y:F6})");
        }
        return true;
    }
    
    /// <summary>
    /// Findet alle MarketTraveler innerhalb eines bestimmten Radius um eine Position.
    /// </summary>
    /// <param name="position">Der Mittelpunkt der Suche.</param>
    /// <param name="radius">Der Suchradius in Metern.</param>
    /// <returns>Eine Liste von MarketTravelern im Umkreis.</returns>
    public List<MarketTraveler> GetTravelersWithinRadius(Position position, double radius)
    {
        if (position == null || _activeTravelers.Count == 0)
        {
            return new List<MarketTraveler>();
        }
        
        return _activeTravelers
            .Where(traveler => traveler.Position != null && position.DistanceInMTo(traveler.Position) < radius)
            .ToList();
    }
    
    public MarketStall? FindNearestStall(Position pos)
    {
        if (_stalls.Count == 0 || pos == null) return null;
        
        return _stalls
            .Where(s => s.Position != null)
            .OrderBy(s => pos.DistanceInMTo(s.Position))
            .FirstOrDefault();    
    }
    
    public bool IsInsideMarketArea(Position p)
    {
        ParsePolygon();
        if (_marketPolygon == null) return false;
        return PolygonUtils.IsPointInPolygon(p.X, p.Y, _marketPolygon);
    }

    public List<(double lon, double lat)> GetMarketPolygon()
    {
        ParsePolygon();
        return _marketPolygon;
    }

    private void ParsePolygon()
    {
        if (_polygonParsed) return;
        _marketPolygon = PolygonUtils.ParsePolygon(TopLeftCorner, TopRightCorner, BottomRightCorner, BottomLeftCorner);
        _polygonParsed = true;
    }

    public long GetCurrentTick() => _currentTick;

    public override void SetCurrentTick(long currentStep)
    {
        _currentTick = currentStep;
        base.SetCurrentTick(currentStep);

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);
        }
    }

    public void EnqueueRegister(ITickClient agent)
    {
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.Add(traveler);
        }
        
        _registerAgent?.Invoke(this, agent);
    }

    public void Unregister(ITickClient agent)
    {
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.Remove(traveler);
        }
        
        _unregisterAgent?.Invoke(this, agent);
    }
    
    public new void DisposeLayer() { }
}