using System.Collections.Generic;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHBusModel.Model;
using SOHBusModel.Station;

namespace SOHBusModel.Route;

public class BusRouteLayer : AbstractLayer, IBusRouteLayer
{
    private Dictionary<string, BusRoute> _busRoutes;

    public BusRouteLayer(BusStationLayer stationLayer)
    {
        BusStationLayer = stationLayer;
    }

    public bool TryGetRoute(string line, out BusRoute busRoute)
    {
        return _busRoutes.TryGetValue(line, out busRoute);
    }

    public BusStationLayer BusStationLayer { get; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        _busRoutes = BusRouteReader.Read(Mapping.File, BusStationLayer);
        return true;
    }
}