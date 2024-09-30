using System;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Multimodal.Model;

namespace SOHVeddelFloodBox;

public class WaterLevelLayer : HumanTravelerLayer, ISteppedActiveLayer
{
    private readonly HeightModel _heightModel = new();
    private readonly OutputBuilder _outputBuilder = OutputBuilder.Builder();
    private bool _hasSimulationEnded;
    private int _simulationTime;
    private double _waterHeight;

    [PropertyDescription(Name = "ticks_before_start")]
    public int TicksBeforeStart { get; set; }

    [PropertyDescription(Name = "height_per_tick")]
    public double HeightPerTick { get; set; }


    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        _heightModel.Read();
        double min = 1000;
        double max = -1000;

        foreach (var node in SpatialGraphMediatorLayer.Environment.Nodes)
        {
            var height = _heightModel.DetectBestHeight(node.Position);
            node.Attributes.Add("height", height);

            if (height > max) max = height;
            if (height < min) min = height;
        }

        Console.WriteLine($"Highest point: {max}");
        Console.WriteLine($"Lowest point: {min}");

        return base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
    }

    public void Tick()
    {
        if (TicksBeforeStart == 0)
            _waterHeight += HeightPerTick;
        else
            TicksBeforeStart -= 1;

        _outputBuilder.AddWaterLevel(_simulationTime, _waterHeight);

        _simulationTime += 1;

        foreach (var node in SpatialGraphMediatorLayer.Environment.Nodes)
            if (node.Attributes.TryGetValue("height", out var height))
            {
                if (!((double)height < _waterHeight) || node.Attributes.TryGetValue("underwater", out _)) continue;

                node.Attributes.Add("underwater", true);

                foreach (var edge in node.IncomingEdges) edge.Value.Attributes.Add("underwater", true);

                foreach (var edge in node.OutgoingEdges) edge.Value.Attributes.Add("underwater", true);
            }

        var countEdgesUnderWater =
            SpatialGraphMediatorLayer.Environment.Edges.Count(edge =>
                edge.Value.Attributes.TryGetValue("underwater", out _));

        Console.WriteLine("Water: {0:#0.00} streets above water level: {1} ",
            _waterHeight, SpatialGraphMediatorLayer.Environment.Edges.Count - countEdgesUnderWater);
    }


    public void PreTick()
    {
    }

    public void PostTick()
    {
        if ((GetCurrentTick() >= 0 && GetCurrentTick() != Context.MaxTicks) || _hasSimulationEnded) return;

        _outputBuilder.BuildWaterCsv();
        _hasSimulationEnded = true;
    }
}