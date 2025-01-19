using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;

namespace SOHModel.Bus.Model;

public class BusLayer : VectorLayer
{
    public BusLayer()
    {
        Driver = new Dictionary<Guid, BusDriver>();
    }

    [PropertyDescription] 
    public IBusRouteLayer BusRouteLayer { get; set; } = default!;

    public IDictionary<Guid, BusDriver> Driver { get; private set; }

    public ISpatialGraphEnvironment GraphEnvironment { get; set; }

    public override bool InitLayer(
        LayerInitData layerInitData, 
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
                        IsBiDirectedImport = true, Modalities = new HashSet<SpatialModalityType>
                        {
                            SpatialModalityType.CarDriving
                        }
                    }
                }
            ]
        });


        Driver = AgentManager.SpawnAgents<BusDriver>(
            layerInitData.AgentInitConfigs.First(mapping => mapping.ModelType.MetaType == typeof(BusDriver)),
            registerAgentHandle, unregisterAgent, new List<ILayer> { this });

        return true;
    }
}