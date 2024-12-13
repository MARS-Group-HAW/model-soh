using System;
using Mars.Interfaces.Data;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;

namespace SOHTests.Commons.Layer;

/// <summary>
/// Base class for bus layer fixtures.
/// It initializes the bus station layer and the bus route layer.
/// </summary>
/// <remarks>
/// In order to use this fixture, you need to create a derived class and pass the file paths of the line schedule and the station file.
/// For an example, see <see cref="BigEventBusLayerFixture"/>.
/// </remarks>
public abstract class BusLayerFixture : IDisposable
{
    public BusLayerFixture(string lineScheduleFilePath, string stationFilePath) {
        CheckInput(new[] { lineScheduleFilePath, stationFilePath });
        LineScheduleFilePath = lineScheduleFilePath;
        StationFilePath = stationFilePath;
        BusStationLayer = new BusStationLayerFixture(stationFilePath).BusStationLayer;
        var busRouteLayer = new BusRouteLayer(BusStationLayer);
        busRouteLayer.InitLayer(
            new LayerInitData
            {
                LayerInitConfig = { File = lineScheduleFilePath }
            }, (_, _) => { }, (_, _) => { });
        BusRouteLayer = busRouteLayer;
    }

    private static void CheckInput(string[] filePath) {
        foreach (var path in filePath) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            }
            if (!System.IO.File.Exists(path)) {
                throw new ArgumentException("File does not exist.", nameof(path));
            }
        }
    }

    public string LineScheduleFilePath { get; }

    public string StationFilePath { get; }

    public BusStationLayer BusStationLayer { get; private set; }

    public IBusRouteLayer BusRouteLayer { get; private set; }

    public void Dispose()
    {
        BusStationLayer.Dispose();
        BusStationLayer = null;
        BusRouteLayer = null;
    }

    private class BusStationLayerFixture : IDisposable
    {
        public BusStationLayerFixture(string filePath)
        {
            BusStationLayer = new BusStationLayer();
            BusStationLayer.InitLayer(
                new LayerInitData
                {
                    LayerInitConfig = { File = filePath }
                });
        }

        public BusStationLayer BusStationLayer { get; }

        public void Dispose()
        {
            BusStationLayer.Dispose();
        }
    }
}
