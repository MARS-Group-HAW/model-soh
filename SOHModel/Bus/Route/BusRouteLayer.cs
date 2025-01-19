using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Bus.Model;
using SOHModel.Bus.Station;

namespace SOHModel.Bus.Route;

public class BusRouteLayer(BusStationLayer stationLayer) : AbstractLayer, IBusRouteLayer
{
    private Dictionary<string, BusRoute> _busRoutes = [];

    public bool TryGetRoute(string line, out BusRoute? busRoute)
    {
        return _busRoutes.TryGetValue(line, out busRoute);
    }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        _busRoutes = BusRouteReader.Read(Mapping.File, stationLayer);
        return true;
    }
}