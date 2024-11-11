using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Domain.Graph;

namespace SOHModel.SemiTruck.Model;

public class SemiTruckLayer : AbstractActiveLayer, ISemiTruckLayer, ISpatialGraphLayer
{
    public SemiTruckLayer()
    {
        Driver = new Dictionary<Guid, SemiTruckDriver>();
    }

    /// <summary>
    ///     A dictionary containing all truck drivers in the layer, mapped by their unique IDs.
    /// </summary>
    public IDictionary<Guid, SemiTruckDriver> Driver { get; private set; }

    /// <summary>
    ///     The spatial environment for all SemiTruck drivers.
    /// </summary>
    public ISpatialGraphEnvironment GraphEnvironment { get; set; }

    // Implement the Environment property required by ISpatialGraphLayer
    ISpatialGraphEnvironment ISpatialGraphLayer.Environment => GraphEnvironment;

    /// <summary>
    ///     Specifies the modality type for the layer (e.g., driving).
    /// </summary>
    public ModalChoice ModalChoice => ModalChoice.CarDriving; // Replace with ModalChoice.TruckDriving if available

    /// <summary>
    ///     Adds a SemiTruckDriver to the layer.
    /// </summary>
    public void AddDriver(SemiTruckDriver driver)
    {
        if (!Driver.ContainsKey(driver.ID))
        {
            Driver[driver.ID] = driver;
        }
    }

    /// <summary>
    ///     Removes a SemiTruckDriver from the layer.
    /// </summary>
    public void RemoveDriver(SemiTruckDriver driver)
    {
        Driver.Remove(driver.ID);
    }

    /// <summary>
    ///     Initializes the SemiTruck layer, setting up the spatial environment and spawning the SemiTruck drivers.
    /// </summary>
    /// <param name="layerInitData">Initial data for the layer.</param>
    /// <param name="registerAgentHandle">Delegate to register agents.</param>
    /// <param name="unregisterAgent">Delegate to unregister agents.</param>
    /// <returns>True if initialization is successful; otherwise, false.</returns>
    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        // Set up the spatial environment (e.g., road network)
        GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports =
            [
                new Input
                {
                    File = layerInitData.LayerInitConfig.File,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving }
                    }
                }
            ]
        });

        // Spawn all SemiTruck drivers
        Driver = AgentManager.SpawnAgents<SemiTruckDriver>(
            layerInitData.AgentInitConfigs.First(config => config.ModelType.MetaType == typeof(SemiTruckDriver)),
            registerAgentHandle, unregisterAgent, new List<ILayer> { this });

        return true;
    }
}
