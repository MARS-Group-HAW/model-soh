using System;
using System.Collections.Generic;
using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHCarModel.Parking;
using SOHDomain.Graph;

namespace SOHCarModel.Model;

public class CarLayer : AbstractActiveLayer, ICarLayer, ISpatialGraphLayer
{
    public CarLayer(ISpatialGraphEnvironment environment = null, ICarParkingLayer carParkingLayer = null)
    {
        Environment = environment;
        CarParkingLayer = carParkingLayer;
    } //TODO empty constructor

    /// <summary>
    ///     Gets all car driver entities of this layer
    /// </summary>
    public IDictionary<Guid, IAgent> Driver { get; private set; }

    /// <summary>
    ///     The car graph environment for all <see cref="CarDriver" />
    /// </summary>
    public ISpatialGraphEnvironment Environment { get; set; }

    public ICarParkingLayer CarParkingLayer { get; set; }

    public ModalChoice ModalChoice => ModalChoice.CarDriving;

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        if (Mapping.Value is ISpatialGraphEnvironment input)
            Environment = input;
        else if (!string.IsNullOrEmpty(Mapping.File))
            Environment = new SpatialGraphEnvironment(layerInitData.LayerInitConfig.File);

        Driver = new Dictionary<Guid, IAgent>();
        foreach (var config in layerInitData.AgentInitConfigs)
            Driver.AddRange(AgentManager.SpawnAgents(config,
                registerAgentHandle, unregisterAgent, new List<ILayer> { this },
                new List<IEnvironment> { Environment }));

        return true;
    }
}