using Mars.Common.Core;
using Mars.Components.Layers.Temporal;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Multimodal.Layers.TrafficLight;

public class TrafficLight : IVectorFeature, IQueryFieldProvider
{
    public TrafficLight()
    {
    }
    
    public TrafficLight(TrafficLightPhase currentLightPhase, int startGreenPhaseTick, int startYellowPhaseTick,
        int startRedPhaseTick)
    {
        TrafficLightPhase = currentLightPhase;
        StartGreenTick = startGreenPhaseTick;
        StartYellowTick = startYellowPhaseTick;
        StartRedTick = startRedPhaseTick;
    }
    
    /// <summary>
    ///     The centroid of this traffic signal.
    /// </summary>
    public Position Position { get; set; } = default!;

    public string? LaneType { get; set; }
    
    public int StartGreenTick { get; }
    public int StartYellowTick { get; }
    public int StartRedTick { get; }
    public TrafficLightPhase TrafficLightPhase { get; set; }
    public VectorStructuredData VectorStructured { get; set; } = default!;
    
    private TrafficSignalLayer Layer { get; set; } = default!;
    
    public void Init(ILayer layer, VectorStructuredData data)
    {
        Layer = (TrafficSignalLayer)layer;
        Init(data);
    }

    public object? GetValue(string field)
    {
        return field == "DataStreamId" ? VectorStructured.Data["streamId"] : null;
    }
    
    public void Update(VectorStructuredData data)
    {
        if (!Layer.IsInitialized)
            Init(data);
        else
        {
            Update(data.Data);
        }
    }

    private void Update(IDictionary<string, object> values)
    {
        if (Layer.SynchronizeAlwaysSince.HasValue &&
            Layer.SynchronizeAlwaysSince >= Layer.Context.CurrentTimePoint)
        {
            if (TrafficLightPhase == TrafficLightPhase.Green)
            {
                TrafficLightPhase = TrafficLightPhase.Red;
            } else if (TrafficLightPhase == TrafficLightPhase.Red)
            {
                TrafficLightPhase = TrafficLightPhase.Green;
            } else if (TrafficLightPhase == TrafficLightPhase.Yellow)
            {
                TrafficLightPhase = TrafficLightPhase.Green;
            }
        }
    }

    private const string LaneTypeKey = "properties/laneType";
    
    private void Init(VectorStructuredData data)
    {
        var centroid = data.Geometry.Centroid;

        Position = Position.CreatePosition(centroid.X, centroid.Y);
        
        LaneType = data.Data.ContainsKey(LaneTypeKey)
            ? data.Data[LaneTypeKey].Value<string>() : "KFZ";
        
        VectorStructured = data;
    }
}