using System;
using Mars.Interfaces.Data;
using SOHTrainModel.Model;
using SOHTrainModel.Route;
using SOHTrainModel.Station;

namespace SOHTests.Commons.Layer;

public class TrainRouteLayerFixture : IDisposable
{
    public TrainRouteLayerFixture()
    {
        TrainStationLayer = new TrainStationLayerFixture().TrainStationLayer;
        var trainRouteLayer = new TrainRouteLayer(TrainStationLayer);
        trainRouteLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = ResourcesConstants.TrainU1LineCsv }
            }, (_, _) => { }, (_, _) => { });
        TrainRouteLayer = trainRouteLayer;
    }

    public TrainStationLayer TrainStationLayer { get; private set; }

    public ITrainRouteLayer TrainRouteLayer { get; private set; }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        TrainStationLayer.Dispose();
        TrainStationLayer = null;
        TrainRouteLayer = null;
    }
}

public class TrainStationLayerFixture : IDisposable
{
    /// <summary>
    ///     Fixture for the <see cref="TrainStationLayerFixture" /> with standard input file.
    /// </summary>
    public TrainStationLayerFixture()
    {
        TrainStationLayer = new TrainStationLayer();
        TrainStationLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = ResourcesConstants.TrainStationsU1 }
            });
    }

    public TrainStationLayer TrainStationLayer { get; }

    public void Dispose()
    {
        TrainStationLayer.Dispose();
    }
}