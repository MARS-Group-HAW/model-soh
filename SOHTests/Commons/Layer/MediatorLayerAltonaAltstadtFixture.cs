using System;
using SOHModel.Multimodal.Layers;
using Xunit;

namespace SOHTests.Commons.Layer;

public class MediatorLayerAltonaAltstadtFixture : IDisposable
{
    public MediatorLayerAltonaAltstadtFixture()
    {
        var gisPoiLayer = TestVectorPoiLayer.Create(ResourcesConstants.PoisAltonaAltstadt);
        var gisLandUseLayer = TestVectorLanduseLayer.Create(ResourcesConstants.LanduseAltonaAltstadt);
        var buildingLayer = TestVectorBuildingsLayer.Create(ResourcesConstants.BuildingsAltonaAltstadt);

        Assert.True(gisPoiLayer.Features.Count > 0);
        Assert.True(gisLandUseLayer.Features.Count > 0);
        Assert.True(buildingLayer.Features.Count > 0);

        MediatorLayer = new MediatorLayer(gisLandUseLayer, gisPoiLayer, buildingLayer);
    }

    public MediatorLayer MediatorLayer { get; }

    public void Dispose()
    {
        MediatorLayer.VectorBuildingsLayer.Dispose();
        MediatorLayer.VectorPoiLayer.Dispose();
        MediatorLayer.VectorLandUseLayer.Dispose();
    }
}