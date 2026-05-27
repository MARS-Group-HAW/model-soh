using System;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Agents;
using SOHModel.ChristmasMarket.Layers;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A singleton observer agent that safely exports aggregated market metrics to PostgreSQL per tick.
/// It observes the MarketLayer.Current and exposes its metrics as PropertyDescriptions.
/// This agent is hosted on the MarketTravelerLayer to ensure it is correctly ticked.
/// </summary>
public class MarketAnalyticsAgent : IAgent<MarketTravelerLayer>, ITickClient
{
    [PropertyDescription]
    public int ActiveVisitors => MarketLayer.Current?.ActiveVisitors ?? 0;

    [PropertyDescription]
    public string MarketStallsJson => MarketLayer.Current?.MarketStallsJson ?? string.Empty;

    public Guid ID { get; set; }

    [PropertyDescription]
    public MarketTravelerLayer Layer { get; set; }

    public void Init(MarketTravelerLayer layer)
    {
        Layer = layer;
    }

    public void Tick()
    {
        Layer?.UnregisterAgent?.Invoke(Layer, this);
    }
}
