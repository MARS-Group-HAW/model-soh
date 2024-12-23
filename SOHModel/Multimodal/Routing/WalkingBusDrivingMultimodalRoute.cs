using Mars.Interfaces.Environments;
using SOHModel.Bus.Station;
using SOHModel.Domain.Graph;

namespace SOHModel.Multimodal.Routing;

public class WalkingBusDrivingMultimodalRoute : MultimodalRoute
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly Position _goal;
    private readonly Position _start;
    private readonly IBusStationLayer _busStationLayer;

    /// <summary>
    ///     Describes a multimodal route with walk, bus drive, walk.
    /// </summary>
    /// <param name="environmentLayer">Contains the environment.</param>
    /// <param name="stationLayer">The station layer containing all bus stations to route to.</param>
    /// <param name="start">Position where the route should start.</param>
    /// <param name="goal">Position where the route should end.</param>
    public WalkingBusDrivingMultimodalRoute(
        ISpatialGraphLayer environmentLayer,
        IBusStationLayer stationLayer, Position start, Position goal)
    {
        _start = start;
        _goal = goal;

        _busStationLayer = stationLayer;
        _environment = environmentLayer.Environment;

        var (startBusStation, routeToFirstStation) = FindStartBusStationAndWalkingRoute();
        var (goalBusStation, routeToGoal) = FindGoalBusStationAndFinalWalkingRoute();
        var busRoutes = FindBusRoutes(startBusStation, goalBusStation).ToList();

        if (busRoutes.Count == 0)
            throw new ArgumentException("Could not find any bus route.");

        if (routeToFirstStation != null) Add(routeToFirstStation, ModalChoice.Walking);
        
        foreach (var busRoute in busRoutes)
        {
            Add(busRoute, ModalChoice.Bus);
        }

        if (routeToGoal != null)
        {
            Add(routeToGoal, ModalChoice.Walking);
        }
    }

    private (BusStation?, Route?) FindStartBusStationAndWalkingRoute(
        HashSet<BusStation>? unreachable = null)
    {
        unreachable ??= new HashSet<BusStation>();
        var busStation = _busStationLayer.Nearest(_start, station => !unreachable.Contains(station));
        if (busStation == null)
            throw new ApplicationException($"No reachable bus station found for route from {_start} to {_goal}");

        var startNode = _environment.NearestNode(_start, SpatialModalityType.Walking);
        var busStationNode = _environment.NearestNode(busStation.Position, SpatialModalityType.Walking);
        if (startNode.Equals(busStationNode))
            return (busStation, null);

        var route = _environment.FindShortestRoute(startNode, busStationNode, WalkingFilter);
        if (route == null) // no walking route exists, Train station is excluded from next search
        {
            unreachable.Add(busStation);
            return FindStartBusStationAndWalkingRoute(unreachable);
        }

        // var distance = startNode.Position.DistanceInMTo(BusStationNode.Position);
        // if (route.RouteLength > distance * 2)
        {
            var nextBusStation = _busStationLayer.Nearest(_start,
                station => !unreachable.Contains(station) && station != busStation);
            if (nextBusStation != null)
            {
                var nextBusStationNode = _environment.NearestNode(nextBusStation.Position);
                if (!startNode.Equals(nextBusStationNode))
                {
                    var nextRoute = _environment.FindShortestRoute(startNode, nextBusStationNode, WalkingFilter);
                    if (nextRoute?.RouteLength < route.RouteLength)
                        return (nextBusStation, nextRoute);
                }
            }
        }

        return (busStation, route);
    }

    private (BusStation, Route) FindGoalBusStationAndFinalWalkingRoute(
        HashSet<BusStation>? unreachable = null)
    {
        unreachable ??= [];
        var busStation = _busStationLayer.Nearest(_goal, station => !unreachable.Contains(station));
        if (busStation == null) {
            throw new ApplicationException(
                $"No bus route available within the spatial graph environment to reach goal station from {_start} to {_goal}");
        }

        var busStationNode = _environment.NearestNode(busStation.Position, SpatialModalityType.Walking);
        var goalNode = _environment.NearestNode(_goal, SpatialModalityType.Walking);
        if (busStationNode.Equals(goalNode))
            return (busStation, null);

        var route = _environment.FindShortestRoute(busStationNode, goalNode, WalkingFilter);
        if (route != null)
        {
            var distance = goalNode.Position.DistanceInMTo(busStationNode.Position);
            if (route.RouteLength > distance * 2)
            {
                var nextBusStation = _busStationLayer.Nearest(_goal,
                    station => !unreachable.Contains(station) && station != busStation);
                if (nextBusStation != null)
                {
                    var nextBusStationNode = _environment.NearestNode(nextBusStation.Position);
                    if (!goalNode.Equals(nextBusStationNode))
                    {
                        var nextRoute =
                            _environment.FindShortestRoute(nextBusStationNode, goalNode, WalkingFilter);
                        if (nextRoute?.RouteLength < route.RouteLength) return (nextBusStation, nextRoute);
                    }
                }
            }

            return (busStation, route);
        }

        unreachable.Add(busStation);
        return FindGoalBusStationAndFinalWalkingRoute(unreachable);
    }

    private IEnumerable<Route> FindBusRoutes(BusStation? startBusStation, BusStation? goalBusStation)
    {
        var busRoutes = new List<Route>();
        var startBusStationNode =
            _environment.NearestNode(startBusStation.Position, SpatialModalityType.CarDriving);
        var goalBusStationNode =
            _environment.NearestNode(goalBusStation.Position, SpatialModalityType.CarDriving);

        if (startBusStationNode.Equals(goalBusStationNode)) return busRoutes;
        var startLines = startBusStation.Lines;
        var goalLines = goalBusStation.Lines;

        if (startLines.Intersect(goalLines).Any()) // find direct line
        {
            var busRoute =
                _environment.FindShortestRoute(startBusStationNode, goalBusStationNode, BusDrivingFilter);
            busRoutes.Add(busRoute);
        }
        else // find line with transfer point
        {
            var transferPoint = _busStationLayer.Nearest(startBusStation.Position,
                station => station.Lines.Intersect(startLines).Any() &&
                           station.Lines.Intersect(goalLines).Any());
            if (transferPoint != null) // single transfer point
            {
                var transferBusStationWaterwayNode =
                    _environment.NearestNode(transferPoint.Position, SpatialModalityType.CarDriving);
                var busRoute1 = _environment.FindShortestRoute(startBusStationNode,
                    transferBusStationWaterwayNode, BusDrivingFilter);
                busRoutes.Add(busRoute1);

                var busRoute2 = _environment.FindShortestRoute(transferBusStationWaterwayNode,
                    goalBusStationNode,
                    BusDrivingFilter);
                busRoutes.Add(busRoute2);
            }
            else // multiple transfer points
            {
                var transferPointsStart = _busStationLayer.Features.OfType<BusStation>().Where(
                    station => station != startBusStation && station.Lines.Intersect(startLines).Any() &&
                               station.Lines.Count > 1);
                var transferPointsGoal = _busStationLayer.Features.OfType<BusStation>().Where(
                    station => station != goalBusStation && station.Lines.Intersect(goalLines).Any() &&
                               station.Lines.Count > 1).ToList();


                foreach (var transferStart in transferPointsStart)
                {
                    foreach (var transferGoal in transferPointsGoal)
                    {
                        if (transferStart.Lines.Intersect(transferGoal.Lines).Any())
                        {
                            var transferStartNode = _environment.NearestNode(transferStart.Position,
                                SpatialModalityType.CarDriving);
                            var transferGoalNode = _environment.NearestNode(transferGoal.Position,
                                SpatialModalityType.CarDriving);

                            var busRoute1 = _environment.FindShortestRoute(startBusStationNode, transferStartNode,
                                BusDrivingFilter);
                            busRoutes.Add(busRoute1);

                            var busRoute2 = _environment.FindShortestRoute(transferStartNode, transferGoalNode,
                                BusDrivingFilter);
                            busRoutes.Add(busRoute2);

                            var busRoute3 = _environment.FindShortestRoute(transferGoalNode, goalBusStationNode,
                                BusDrivingFilter);
                            busRoutes.Add(busRoute3);
                        }
                    }
                }
            }
        }

        return busRoutes;

        static bool BusDrivingFilter(ISpatialEdge edge)
        {
            return edge.Modalities.Contains(SpatialModalityType.CarDriving);
        }
    }
}