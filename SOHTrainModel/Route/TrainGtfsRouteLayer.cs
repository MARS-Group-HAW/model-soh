using System.Collections.Generic;
using System.Linq;
using GTFS;
using GTFS.Entities;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHTrainModel.Model;
using SOHTrainModel.Station;

namespace SOHTrainModel.Route;

public class TrainGtfsRouteLayer : AbstractLayer, ITrainRouteLayer
{
    private GTFSFeed _feed;


    public Dictionary<string, TrainRoute> Routes { get; private set; }
    public TrainStationLayer TrainStationLayer { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        var result = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        Routes = new Dictionary<string, TrainRoute>();

        var reader = new GTFSReader<GTFSFeed>();
        _feed = reader.Read(layerInitData.LayerInitConfig.File);

        return result;
    }

    public bool TryGetRoute(string line, out TrainRoute trainRoute)
    {
        if (!Routes.ContainsKey(line))
        {
            trainRoute = FindTrainRoute(line);
            if (trainRoute == null) return false;

            Routes.Add(line, trainRoute);
        }

        trainRoute = Routes[line];
        return true;
    }

    private TrainRoute FindTrainRoute(string routeShortName)
    {
        var trainRoute = new TrainRoute();

        var route = _feed.Routes.Get().FirstOrDefault(route => route.ShortName.Equals(routeShortName));
        if (route == null) return null;

        var trip = _feed.Trips.Get().FirstOrDefault(trip => trip.RouteId == route.Id);
        if (trip == null) return null;

        using var stopTimes = _feed.StopTimes.GetForTrip(trip.Id).GetEnumerator();

        StopTime lastStopTime = null;
        if (stopTimes.MoveNext()) lastStopTime = stopTimes.Current;

        while (stopTimes.MoveNext())
        {
            var nextStopTime = stopTimes.Current;
            var lastStop = _feed.Stops.Get(lastStopTime?.StopId);
            var nextStop = _feed.Stops.Get(nextStopTime?.StopId);

            if (lastStopTime != null && lastStopTime.DepartureTime != null && nextStopTime != null &&
                nextStopTime.ArrivalTime != null)
            {
                var minutes = CalculateTravelTime(lastStopTime.DepartureTime.Value, nextStopTime.ArrivalTime.Value);

                var startStation =
                    TrainStationLayer.Nearest(Position.CreateGeoPosition(lastStop.Longitude, lastStop.Latitude));
                var goalStation =
                    TrainStationLayer.Nearest(Position.CreateGeoPosition(nextStop.Longitude, nextStop.Latitude));
                if (startStation != null && goalStation != null && startStation != goalStation)
                    trainRoute.Entries.Add(new TrainRouteEntry(startStation, goalStation, minutes));
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