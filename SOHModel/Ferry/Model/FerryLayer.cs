using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Ferry.Route;

namespace SOHModel.Ferry.Model;

public class FerryLayer(FerryRouteLayer layer) : VectorLayer
{
    public FerryRouteLayer FerryRouteLayer { get; } = layer;

    public IDictionary<Guid, FerryDriver> Driver { get; private set; } = new Dictionary<Guid, FerryDriver>();

    public ISpatialGraphEnvironment GraphEnvironment { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports =
            [
                new Input
                {
                    File = layerInitData.LayerInitConfig.File,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true
                    }
                }
            ]
        });

        Driver = AgentManager.SpawnAgents<FerryDriver>(
            layerInitData.AgentInitConfigs.First(mapping => mapping.ModelType.MetaType == typeof(FerryDriver)),
            registerAgentHandle, unregisterAgent, new List<ILayer> { this });
        return true;
    }
}