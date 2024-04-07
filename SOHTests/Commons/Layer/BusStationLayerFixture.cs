using System;
using Mars.Interfaces.Data;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;

namespace SOHTests.Commons.Layer;

public class BusRouteLayerFixture : IDisposable
{
    public BusRouteLayerFixture()
    {
        BusStationLayer = new BusStationLayerFixture().BusStationLayer;
        var busRouteLayer = new BusRouteLayer(BusStationLayer);
        busRouteLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = ResourcesConstants.Bus113LineCsv }
            }, (_, _) => { }, (_, _) => { });
        BusRouteLayer = busRouteLayer;
    }

    public BusStationLayer BusStationLayer { get; private set; }

    public IBusRouteLayer BusRouteLayer { get; private set; }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        BusStationLayer.Dispose();
        BusStationLayer = null;
        BusRouteLayer = null;
    }

    private class BusStationLayerFixture : IDisposable
    {
        /// <summary>
        ///     Fixture for the <see cref="BusStationLayerFixture" /> with given input file.
        /// </summary>
        /// <param name="filePath">Defines the import data. If <code>null</code>, then use Hamburg ferry stations.</param>
        public BusStationLayerFixture(string filePath = null)
        {
            BusStationLayer = new BusStationLayer();
            BusStationLayer.InitLayer(
                new LayerInitData
                {
                    LayerInitConfig = { File = filePath ?? ResourcesConstants.BusStations113 }
                });
        }

        public BusStationLayer BusStationLayer { get; }

        public void Dispose()
        {
            BusStationLayer.Dispose();
        }
    }
}