using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model.Options;

namespace SOHDomain.Graph;

/// <summary>
///     This layer provides the required <see cref="ISpatialGraphEnvironment" /> for the whole model.
/// </summary>
public class SpatialGraphMediatorLayer : AbstractLayer, ISpatialGraphLayer, IDataLayer
{
    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var inputs = layerInitData.LayerInitConfig.Inputs;

        if (inputs != null && inputs.Any())
            Environment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = inputs
            });

        return true;
    }

    /// <summary>
    ///     Holds a merged multi-modal spatial graph environment
    /// </summary>
    public ISpatialGraphEnvironment Environment { get; set; }
}