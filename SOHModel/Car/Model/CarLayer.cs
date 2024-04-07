using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;

namespace SOHModel.Car.Model;

public class CarLayer : AbstractActiveLayer, ICarLayer, ISpatialGraphLayer
{
    public CarLayer(
        ISpatialGraphEnvironment? environment = null, 
        ICarParkingLayer? carParkingLayer = null)
    {
        Environment = environment;
        CarParkingLayer = carParkingLayer;
        Driver = new Dictionary<Guid, IAgent>();
    }

    /// <summary>
    ///     Gets all car driver entities of this layer
    /// </summary>
    public IDictionary<Guid, IAgent> Driver { get; private set; }

    /// <summary>
    ///     The car graph environment for all <see cref="CarDriver" />
    /// </summary>
    public ISpatialGraphEnvironment? Environment { get; set; }

    public ICarParkingLayer? CarParkingLayer { get; set; }

    public ModalChoice ModalChoice => ModalChoice.CarDriving;

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        if (Mapping.Value is ISpatialGraphEnvironment input)
            Environment = input;
        else if (!string.IsNullOrEmpty(Mapping.File))
            Environment = new SpatialGraphEnvironment(layerInitData.LayerInitConfig.File);

        foreach (var config in layerInitData.AgentInitConfigs)
        {
            if (registerAgentHandle != null && unregisterAgent != null && Environment != null)
            {
                Driver.AddRange(AgentManager.SpawnAgents(config,
                    registerAgentHandle, unregisterAgent, [this], [Environment]));
            }
        }

        return true;
    }
}