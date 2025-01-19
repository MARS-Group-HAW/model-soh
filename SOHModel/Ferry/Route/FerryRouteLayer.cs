using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Ferry.Station;

namespace SOHModel.Ferry.Route;

public class FerryRouteLayer(FerryStationLayer stationLayer) : AbstractLayer
{
    public readonly FerryStationLayer StationLayer = stationLayer;

    public Dictionary<int, FerryRoute> FerryRoutes { get; private set; } = new();

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        FerryRoutes = FerryRouteReader.Read(Mapping.File, StationLayer);

        return true;
    }
}