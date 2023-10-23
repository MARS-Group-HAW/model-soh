using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core;
using Mars.Common.Core.Logging;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHCarModel.Parking;
using SOHMultimodalModel.Layers;
using SOHMultimodalModel.Multimodal;

namespace SOHMultimodalModel.Model;

public class CitizenLayer : AbstractMultimodalLayer
{
    private static readonly ILogger Logger = LoggerFactory.GetLogger(typeof(CitizenLayer));

    public IDictionary<Guid, Citizen> Agents;

    public CitizenLayer()
    {
        Agents = new ConcurrentDictionary<Guid, Citizen>();
    }

    public MediatorLayer MediatorLayer { get; set; }

    public new CarParkingLayer CarParkingLayer { get; set; }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var agentInitConfig = layerInitData.AgentInitConfigs.FirstOrDefault();
        if (agentInitConfig?.IndividualMapping == null) return false;

        var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        var dependencies = new List<IModelObject>
        {
            MediatorLayer, SpatialGraphMediatorLayer, CarParkingLayer, BicycleRentalLayer, FerryStationLayer
        };

        Agents = agentManager.Spawn<Citizen, CitizenLayer>(dependencies)
            .ToDictionary(citizen => citizen.ID, citizen => citizen);

        var layerParameters = layerInitData.LayerInitConfig.ParameterMapping;
        if (layerParameters.TryGetValue("ParkingOccupancy", out var mapping))
        {
            var occupiedParkingPercentage = mapping.Value.Value<double>();
            var carCount = Agents.Values.Count(p => p.CapabilityDrivingOwnCar);
            CarParkingLayer?.UpdateOccupancy(occupiedParkingPercentage, carCount);
        }

        Logger.LogInfo("Created Agents: " + Agents.Count);

        return Agents.Count != 0;
    }
}