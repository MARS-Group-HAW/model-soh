using System;
using Mars.Interfaces.Data;
using SOHFerryModel.Route;
using SOHFerryModel.Station;

namespace SOHTests.Commons.Layer;

public class FerryRouteLayerFixture : IDisposable
{
    public FerryRouteLayerFixture()
    {
        FerryStationLayer = new FerryStationLayerFixture().FerryStationLayer;
        FerryRouteLayer = new FerryRouteLayer(FerryStationLayer);
        FerryRouteLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = ResourcesConstants.FerryLineCsv }
            }, (_, _) => { }, (_, _) => { });
    }

    public FerryStationLayer FerryStationLayer { get; private set; }

    public FerryRouteLayer FerryRouteLayer { get; private set; }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        FerryStationLayer.Dispose();
        FerryStationLayer = null;
        FerryRouteLayer = null;
    }

    private class FerryStationLayerFixture : IDisposable
    {
        /// <summary>
        ///     Fixture for the <see cref="FerryStationLayerFixture" /> with given input file.
        /// </summary>
        /// <param name="filePath">Defines the import data. If <code>null</code>, then use Hamburg ferry stations.</param>
        public FerryStationLayerFixture(string filePath = null)
        {
            FerryStationLayer = new FerryStationLayer();
            FerryStationLayer.InitLayer(
                new LayerInitData
                {
                    LayerInitConfig = { File = filePath ?? ResourcesConstants.FerryStations }
                });
        }

        public FerryStationLayer FerryStationLayer { get; }

        public void Dispose()
        {
            FerryStationLayer.Dispose();
        }
    }
}