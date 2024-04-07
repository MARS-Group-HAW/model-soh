using GTFS;
using GTFS.Entities;
using GTFS.IO;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Bus.Model;
using SOHModel.Bus.Station;

namespace SOHModel.Bus.Route;

public class BusGtfsRouteLayer : AbstractLayer, IBusRouteLayer
{
    private GTFSFeed _feed;
    
    public Dictionary<string, BusRoute> Routes { get; private set; }
    public BusStationLayer BusStationLayer { get; set; }

    public bool TryGetRoute(string line, out BusRoute busRoute)
    {
        if (!Routes.ContainsKey(line))
        {
            busRoute = FindTrainRoute(line);
            if (busRoute == null) return false;

            Routes.Add(line, busRoute);
        }

        busRoute = Routes[line];
        return true;
    }

    public override bool InitLayer(LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var result = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        Routes = new Dictionary<string, BusRoute>();

        var reader = new GTFSReader<GTFSFeed>();
        _feed = reader.Read(layerInitData.LayerInitConfig.File);

        return result;
    }

    private BusRoute? FindTrainRoute(string routeShortName)
    {
        var trainRoute = new BusRoute();

        var route = _feed.Routes.Get().FirstOrDefault(route => route.ShortName.Equals(routeShortName));
        if (route == null) return null;

        var trip = _feed.Trips.Get().FirstOrDefault(trip => trip.RouteId == route.Id);
        if (trip == null) return null;

        using var stopTimes = _feed.StopTimes.GetForTrip(trip.Id).GetEnumerator();

        StopTime? lastStopTime = null;
        if (stopTimes.MoveNext()) lastStopTime = stopTimes.Current;

        while (stopTimes.MoveNext())
        {
            var nextStopTime = stopTimes.Current;
            var lastStop = _feed.Stops.Get(lastStopTime?.StopId);
            var nextStop = _feed.Stops.Get(nextStopTime?.StopId);

            if (lastStopTime is { DepartureTime.TotalSeconds: > 0 } && nextStopTime is { ArrivalTime.TotalSeconds: > 0 })
            {
                var minutes = CalculateTravelTime(lastStopTime.DepartureTime, nextStopTime.ArrivalTime);

                var startStation =
                    BusStationLayer.Nearest(Position.CreateGeoPosition(lastStop.Longitude, lastStop.Latitude));
                var goalStation =
                    BusStationLayer.Nearest(Position.CreateGeoPosition(nextStop.Longitude, nextStop.Latitude));
                if (startStation != null && goalStation != null)
                    trainRoute.Entries.Add(new BusRouteEntry(startStation, goalStation, minutes));
            }

            lastStopTime = nextStopTime;
        }

        return trainRoute;
    }

    private static int CalculateTravelTime(TimeOfDay departureTime, TimeOfDay arrivalTime)
    {
        return (int)((arrivalTime.TotalSeconds - departureTime.TotalSeconds) / 60d);
    }
}