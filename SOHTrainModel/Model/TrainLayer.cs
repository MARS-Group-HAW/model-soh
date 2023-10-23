using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;

namespace SOHTrainModel.Model;

public class TrainLayer : VectorLayer
{
    public TrainLayer(ITrainRouteLayer layer)
    {
        TrainRouteLayer = layer;
        Driver = new Dictionary<Guid, TrainDriver>();
    }

    public ITrainRouteLayer TrainRouteLayer { get; }

    public IDictionary<Guid, TrainDriver> Driver { get; private set; }

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

        Driver = AgentManager.SpawnAgents<TrainDriver>(
            layerInitData.AgentInitConfigs.First(mapping => mapping.ModelType.MetaType == typeof(TrainDriver)),
            registerAgentHandle, unregisterAgent, new List<ILayer> { this });

        return true;
    }
}