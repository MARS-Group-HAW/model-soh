using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;

namespace SOHModel.Domain.Graph;

/// <summary>
///     This layer provides the <see cref="ISpatialGraphEnvironment" /> for walking entities.
///     Can be used with <c>IMultimodalLayer</c> to combine multiple environments.
/// </summary>
public class SidewalkLayer : AbstractLayer, IDataLayer, ISpatialGraphLayer
{
    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        Environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports =
                Mapping.Inputs.Do(input =>
                {
                    input.InputConfiguration ??= new InputConfiguration { IsBiDirectedImport = true };
                    input.InputConfiguration.Modalities = new HashSet<SpatialModalityType>
                        { SpatialModalityType.Walking, SpatialModalityType.Cycling };
                }).ToList()
        });

        return true;
    }

    public ISpatialGraphEnvironment Environment { get; set; }
}