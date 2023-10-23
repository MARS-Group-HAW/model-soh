using System.Collections.Generic;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHTrainModel.Model;
using SOHTrainModel.Station;

namespace SOHTrainModel.Route;

public class TrainRouteLayer : AbstractLayer, ITrainRouteLayer
{
    private Dictionary<string, TrainRoute> _trainRoutes;

    public TrainRouteLayer(TrainStationLayer stationLayer)
    {
        TrainStationLayer = stationLayer;
    }

    public bool TryGetRoute(string line, out TrainRoute trainRoute)
    {
        return _trainRoutes.TryGetValue(line, out trainRoute);
    }

    public TrainStationLayer TrainStationLayer { get; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        _trainRoutes = TrainRouteReader.Read(Mapping.File, TrainStationLayer);
        return true;
    }
}