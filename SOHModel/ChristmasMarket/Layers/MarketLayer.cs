using System.Collections.Concurrent;
using Mars.Components.Layers;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.ChristmasMarket.Entities;
using SOHModel.ChristmasMarket.Utils;

namespace SOHModel.ChristmasMarket.Layers;

/// <summary>
/// Manages the static environment of the Christmas market, including the market stalls and geographic boundaries.
/// This layer is responsible for loading environmental data from vector files and providing it to agents.
/// It uses the 'Current' property for easy global access (Singleton).
/// </summary>
public class MarketLayer : VectorLayer, ILayer
{
    [PropertyDescription(Name = "topLeftCorner")]
    public string TopLeftCorner { get; set; }

    [PropertyDescription(Name = "topRightCorner")]
    public string TopRightCorner { get; set; }

    [PropertyDescription(Name = "bottomRightCorner")]
    public string BottomRightCorner { get; set; }

    [PropertyDescription(Name = "bottomLeftCorner")]
    public string BottomLeftCorner { get; set; }

    public static MarketLayer Current { get; private set; }
    private long _currentTick;

    private RegisterAgent _registerAgent;
    private UnregisterAgent _unregisterAgent;

    private readonly ConcurrentDictionary<MarketTraveler, byte> _activeTravelers = new();
    private static readonly ConcurrentQueue<ITickClient> PendingRegistrations = new();

    public ISimulationContext Context { get; private set; }
    private readonly List<MarketStall> _stalls = new();
    public IReadOnlyList<MarketStall> Stalls => _stalls;

    [PropertyDescription]
    public int ActiveVisitors => _activeTravelers.Count;

    [PropertyDescription]
    public string MarketStallsJson => System.Text.Json.JsonSerializer.Serialize(_stalls.Select(s => new {
        Name = s.StallName,
        X = s.Position?.X,
        Y = s.Position?.Y
    }));

    private List<(double lon, double lat)> _marketPolygon;
    private bool _polygonParsed;

    /// <summary>
    /// Initializes the layer by loading market stalls from the provided data source.
    /// It parses stall properties like name and type and sets up the simulation context.
    /// </summary>
    /// <param name="layerInitData">Initialization data from the simulation framework.</param>
    /// <param name="registerAgentHandle">Delegate for registering agents.</param>
    /// <param name="unregisterAgentHandle">Delegate for unregistering agents.</param>
    /// <returns>True if initialization is successful.</returns>
    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgentHandle)
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
            
            // Set capacity and service time based on type
            switch (stall.Type)
            {
                case MarketStallType.Glühwein:
                    stall.Capacity = 4;
                    stall.ServiceTime = 15; // ~15 seconds
                    break;
                case MarketStallType.Gastronomie:
                    stall.Capacity = 2;
                    stall.ServiceTime = 30; // ~30 seconds
                    break;
                case MarketStallType.Verkaufsstand:
                    stall.Capacity = 1;
                    stall.ServiceTime = 20;
                    break;
                case MarketStallType.Toilette:
                    stall.Capacity = 2;
                    stall.ServiceTime = 45;
                    break;
                case MarketStallType.Geldautomat:
                    stall.Capacity = 1;
                    stall.ServiceTime = 30;
                    break;
                case MarketStallType.Feuertonne:
                    stall.Capacity = 6;
                    stall.ServiceTime = 60; // Stay for ~1 minute
                    break;
                case MarketStallType.Bühne:
                    stall.Capacity = 10000; // Infinite
                    stall.ServiceTime = 0; // Continuous
                    break;
                default:
                    stall.Capacity = 1;
                    stall.ServiceTime = 10;
                    break;
            }
            
            _stalls.Add(stall);
        }

        // Console.WriteLine($"[INFO] Loaded and initialized {_stalls.Count} market stalls");
        foreach (var stall in _stalls)
        {
            // Console.WriteLine(
            //     $"[INFO] Stall '{stall.StallName}' of type '{stall.Type}' at ({stall.Position.X:F6}, {stall.Position.Y:F6})");
        }

        return true;
    }

    /// <summary>
    /// Finds all MarketTraveler agents within the radius of a given position.
    /// </summary>
    /// <param name="position">The center of the search area.</param>
    /// <param name="radius">The search radius in meters.</param>
    /// <returns>A list of MarketTraveler agents within the specified radius.</returns>
    public List<MarketTraveler> GetTravelersWithinRadius(Position position, double radius)
    {
        if (position == null || _activeTravelers.Count == 0)
        {
            return new List<MarketTraveler>();
        }

        return _activeTravelers.Keys
            .Where(traveler => traveler.Position != null && position.DistanceInMTo(traveler.Position) < radius)
            .ToList();
    }

    /// <summary>
    /// Finds the closest MarketStall to a given position.
    /// </summary>
    /// <param name="pos">The position from which to search.</param>
    /// <returns>The nearest MarketStall, or null if no stalls exist.</returns>
    public MarketStall? FindNearestStall(Position pos)
    {
        if (_stalls.Count == 0 || pos == null) return null;

        return _stalls
            .Where(s => s.Position != null)
            .OrderBy(s => pos.DistanceInMTo(s.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Checks if a given position is within the defined market boundaries.
    /// </summary>
    /// <param name="p">The position to check.</param>
    /// <returns>True if the point is inside the market area, false if it is not.</returns>
    public bool IsInsideMarketArea(Position p)
    {
        ParsePolygon();
        if (_marketPolygon == null) return false;
        return PolygonUtils.IsPointInPolygon(p.X, p.Y, _marketPolygon);
    }

    /// <summary>
    /// Gives back the market's polygon.
    /// </summary>
    /// <returns>A list of (longitude, latitude) tuples representing the polygon's vertices.</returns>
    public List<(double lon, double lat)> GetMarketPolygon()
    {
        ParsePolygon();
        return _marketPolygon;
    }

    /// <summary>
    /// Parses the corner strings into a polygon.
    /// </summary>
    private void ParsePolygon()
    {
        if (_polygonParsed) return;
        _marketPolygon = PolygonUtils.ParsePolygon(TopLeftCorner, TopRightCorner, BottomRightCorner, BottomLeftCorner);
        _polygonParsed = true;
    }

    /// <summary>
    /// Gets the current tick of the simulation.
    /// </summary>
    /// <returns>The current simulation tick as a long integer.</returns>
    public long GetCurrentTick() => _currentTick;

    /// <summary>
    /// Sets the current simulation tick and processes any pending agent registrations.
    /// </summary>
    /// <param name="currentStep">The new current tick value.</param>
    public override void SetCurrentTick(long currentStep)
    {
        _currentTick = currentStep;
        base.SetCurrentTick(currentStep);

        // Tick all stalls to process queues
        foreach (var stall in _stalls)
        {
            stall.Tick();
        }

        while (PendingRegistrations.TryDequeue(out var agent))
        {
            _registerAgent?.Invoke(this, agent);
        }
    }

    /// <summary>
    /// Registers an agent with the simulation and adds it to the layer's internal list.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    public void EnqueueRegister(ITickClient agent)
    {
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.TryAdd(traveler, 0);
        }

        _registerAgent?.Invoke(this, agent);
    }

    /// <summary>
    /// Unregisters an agent from the simulation and removes it from the layer's internal list.
    /// </summary>
    /// <param name="agent">The agent to unregister.</param>
    public void Unregister(ITickClient agent)
    {
        if (agent is MarketTraveler traveler)
        {
            _activeTravelers.TryRemove(traveler, out _);
        }

        _unregisterAgent?.Invoke(this, agent);
    }

    /// <summary>
    /// Disposes the layer.
    /// </summary>
    public new void DisposeLayer() {}
}