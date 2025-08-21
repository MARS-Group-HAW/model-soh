using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Tram.Model;
using SOHModel.Tram.Station;
namespace SOHModel.Tram.Route;

public class TramRouteLayer(TramStationLayer stationLayer) : AbstractLayer, ITramRouteLayer
{
    private Dictionary<string, TramRoute> _tramRoutes = [];

    public bool TryGetRoute(string line, out TramRoute? tramRoute)
    {
        return _tramRoutes.TryGetValue(line, out tramRoute);
    }

    public TramStationLayer TramStationLayer { get; } = stationLayer;

    public override bool InitLayer(LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        _tramRoutes = TramRouteReader.Read(Mapping.File, TramStationLayer);
        return true;
    }
}