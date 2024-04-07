using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Bus.Model;
using SOHModel.Bus.Station;

namespace SOHModel.Bus.Route;



public class BusRouteLayer : AbstractLayer, IBusRouteLayer
{
    private Dictionary<string, BusRoute>? _busRoutes;

    public BusRouteLayer(BusStationLayer stationLayer, 
        Dictionary<string, BusRoute>? busRoutes = null)
    {
        BusStationLayer = stationLayer;
        _busRoutes = busRoutes;
    }

    public bool TryGetRoute(string line, out BusRoute? busRoute)
    {
        busRoute = null;
        return _busRoutes != null && _busRoutes.TryGetValue(line, out busRoute);
    }

    public BusStationLayer BusStationLayer { get; }

    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        _busRoutes = BusRouteReader.Read(Mapping.File, BusStationLayer);
        return true;
    }
}