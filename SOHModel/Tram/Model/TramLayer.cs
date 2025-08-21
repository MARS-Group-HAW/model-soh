using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
namespace SOHModel.Tram.Model;

public class TramLayer: VectorLayer
{
    public TramLayer(ITramRouteLayer layer)
    {
        TramRouteLayer = layer;
        Driver = new Dictionary<Guid, TramDriver>();
    }

    public ITramRouteLayer TramRouteLayer { get; }

    public IDictionary<Guid, TramDriver> Driver { get; private set; }

    public ISpatialGraphEnvironment GraphEnvironment { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = layerInitData.LayerInitConfig.File,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true
                    }
                }
            }
        });

        Driver = AgentManager.SpawnAgents<TramDriver>(
            layerInitData.AgentInitConfigs.First(mapping => mapping.ModelType.MetaType == typeof(TramDriver)),
            registerAgentHandle, unregisterAgent, new List<ILayer> { this });

        return true;
    }
}