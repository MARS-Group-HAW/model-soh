using GTFS;
using GTFS.Entities;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Train.Model;
using SOHModel.Train.Station;
using Extensions = GTFS.IO.Extensions;

namespace SOHModel.Train.Route;

public class TrainGtfsRouteLayer : AbstractLayer, ITrainRouteLayer
{
    private GTFSFeed? _feed;
    private readonly Dictionary<string, TrainRoute> _routes = new();
    
    public TrainStationLayer? TrainStationLayer { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var result = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        var reader = new GTFSReader<GTFSFeed>();
        
        _feed = Extensions.Read(reader, layerInitData.LayerInitConfig.File);
        
        return result;
    }

    public bool TryGetRoute(string line, out TrainRoute? trainRoute)
    {
        if (!_routes.TryGetValue(line, out var value))
        {
            trainRoute = FindTrainRoute(line);
            if (trainRoute == null) return false;
            value = trainRoute;
            _routes.Add(line, value);
        }

        trainRoute = value;
        return true;
    }

    private TrainRoute? FindTrainRoute(string routeShortName)
    {
        ArgumentNullException.ThrowIfNull(TrainStationLayer);
        ArgumentNullException.ThrowIfNull(_feed);
        
        var trainRoute = new TrainRoute();
        
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

            if (lastStopTime is { DepartureTime.TotalSeconds: > 0 } && 
                nextStopTime is { ArrivalTime.TotalSeconds: > 0 })
            {
                var minutes = CalculateTravelTime(lastStopTime.DepartureTime, nextStopTime.ArrivalTime);
                var startStation = TrainStationLayer.Nearest(Position.CreateGeoPosition(lastStop.Longitude, lastStop.Latitude));
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