using System;
using System.IO;
using Mars.Common.Core;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;

namespace SOHTests.Commons;

public class StaticTrafficLightLayer : ISteppedActiveLayer
{
    private readonly CarLayer _carLayer;
    private string _initConfig;
    private bool _initialized;

    public StaticTrafficLightLayer(CarLayer carLayer)
    {
        _carLayer = carLayer;
    }

    public bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgent)
    {
        _initConfig = layerInitData.LayerInitConfig.File;
        return true;
    }

    public long GetCurrentTick()
    {
        //do nothing
        return 0;
    }

    public void SetCurrentTick(long currentStep)
    {
        //do nothing
    }

    public void Tick()
    {
        //do nothing
    }

    public void PreTick()
    {
        if (_initialized) return;

        _initialized = true;
        var lines = File.ReadAllLines(_initConfig);

        if (lines.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(lines),
                "Input file for traffic lights must at least contain one line with lat,lon and desired state");

        foreach (var line in lines)
        {
            var lineContent = line.Split(',');
            var state = lineContent[2].Value<int>();
            var lat = lineContent[0].Value<double>();
            var lon = lineContent[1].Value<double>();
            var position = Position.CreateGeoPosition(lon, lat);

            var node = _carLayer.Environment.NearestNode(position);

            var distance = position.DistanceInMTo(node.Position);
            if (distance > 10)
                throw new ArgumentOutOfRangeException(nameof(distance), "Found node was more than 10m away");

            node.NodeGuard = new StaticTrafficLightController(state);
        }
    }

    public void PostTick()
    {
        //do nothing
    }
}

public class StaticTrafficLightController : INodeGuard
{
    private readonly TrafficLightPhase _trafficLightPhase;

    public StaticTrafficLightController(int state)
    {
        _trafficLightPhase = state switch
        {
            3 => TrafficLightPhase.Red,
            2 => TrafficLightPhase.Yellow,
            1 => TrafficLightPhase.Green,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public TrafficLightPhase GetTrafficLightPhase(ISpatialEdge from, ISpatialEdge to)
    {
        return _trafficLightPhase;
    }

    public bool AccessEdge(long tick, ISpatialEdge from, ISpatialEdge to)
    {
        return GetTrafficLightPhase(from, to) != TrafficLightPhase.Red;
    }
}