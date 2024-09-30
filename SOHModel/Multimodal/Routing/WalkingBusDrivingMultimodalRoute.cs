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
    ///     Describes a multimodal route with walk, Train drive, walk.
    /// </summary>
    /// <param name="environmentLayer">Contains the environment.</param>
    /// <param name="stationLayer">The station layer containing all Train stations to route to.</param>
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

        var (startBusStation, routeToFirstStation) = FindStartTrainStationAndWalkingRoute();
        var (goalBusStation, routeToGoal) = FindGoalTrainStationAndFinalWalkingRoute();
        var trainRoutes = FindBusRoutes(startBusStation, goalBusStation).ToList();

        if (trainRoutes.Count == 0)
            throw new ArgumentException("Could not find any train route.");

        if (routeToFirstStation != null) Add(routeToFirstStation, ModalChoice.Walking);
        
        foreach (var trainRoute in trainRoutes)
        {
            Add(trainRoute, ModalChoice.Train);
        }

        if (routeToGoal != null)
        {
            Add(routeToGoal, ModalChoice.Walking);
        }
    }

    private (BusStation?, Route?) FindStartTrainStationAndWalkingRoute(
        HashSet<BusStation>? unreachable = null)
    {
        unreachable ??= new HashSet<BusStation>();
        var busStation = _busStationLayer.Nearest(_start, station => !unreachable.Contains(station));
        if (busStation == null)
            throw new ApplicationException($"No reachable Train station found for route from {_start} to {_goal}");

        var startNode = _environment.NearestNode(_start, SpatialModalityType.Walking);
        var trainStationNode = _environment.NearestNode(busStation.Position, SpatialModalityType.Walking);
        if (startNode.Equals(trainStationNode))
            return (busStation, null);

        var route = _environment.FindShortestRoute(startNode, trainStationNode, WalkingFilter);
        if (route == null) // no walking route exists, Train station is excluded from next search
        {
            unreachable.Add(busStation);
            return FindStartTrainStationAndWalkingRoute(unreachable);
        }

        // var distance = startNode.Position.DistanceInMTo(TrainStationNode.Position);
        // if (route.RouteLength > distance * 2)
        {
            var nextBusStation = _busStationLayer.Nearest(_start,
                station => !unreachable.Contains(station) && station != busStation);
            if (nextBusStation != null)
            {
                var nextTrainStationNode = _environment.NearestNode(nextBusStation.Position);
                if (!startNode.Equals(nextTrainStationNode))
                {
                    var nextRoute = _environment.FindShortestRoute(startNode, nextTrainStationNode, WalkingFilter);
                    if (nextRoute?.RouteLength < route.RouteLength)
                        return (nextBusStation, nextRoute);
                }
            }
        }

        return (busStation, route);
    }

    private (BusStation, Route) FindGoalTrainStationAndFinalWalkingRoute(
        HashSet<BusStation>? unreachable = null)
    {
        unreachable ??= [];
        var busStation = _busStationLayer.Nearest(_goal, station => !unreachable.Contains(station));
        if (busStation == null)
            throw new ApplicationException(
                $"No Train route available within the spatial graph environment to reach goal station from {_start} to {_goal}");

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
                    var nextTrainStationNode = _environment.NearestNode(nextBusStation.Position);
                    if (!goalNode.Equals(nextTrainStationNode))
                    {
                        var nextRoute =
                            _environment.FindShortestRoute(nextTrainStationNode, goalNode, WalkingFilter);
                        if (nextRoute?.RouteLength < route.RouteLength) return (nextBusStation, nextRoute);
                    }
                }
            }

            return (busStation, route);
        }

        unreachable.Add(busStation);
        return FindGoalTrainStationAndFinalWalkingRoute(unreachable);
    }

    private IEnumerable<Route> FindBusRoutes(BusStation? startBusStation, BusStation? goalBusStation)
    {
        var trainRoutes = new List<Route>();
        var startTrainStationNode =
            _environment.NearestNode(startBusStation.Position, SpatialModalityType.TrainDriving);
        var goalTrainStationNode =
            _environment.NearestNode(goalBusStation.Position, SpatialModalityType.TrainDriving);

        if (startTrainStationNode.Equals(goalTrainStationNode)) return trainRoutes;

        var startLines = startBusStation.Lines;
        var goalLines = goalBusStation.Lines;

        if (startLines.Intersect(goalLines).Any()) // find direct line
        {
            var trainRoute =
                _environment.FindShortestRoute(startTrainStationNode, goalTrainStationNode, TrainDrivingFilter);
            trainRoutes.Add(trainRoute);
        }
        else // find line with transfer point
        {
            var transferPoint = _busStationLayer.Nearest(startBusStation.Position,
                station => station.Lines.Intersect(startLines).Any() &&
                           station.Lines.Intersect(goalLines).Any());
            if (transferPoint != null) // single transfer point
            {
                var transferTrainStationWaterwayNode =
                    _environment.NearestNode(transferPoint.Position, SpatialModalityType.TrainDriving);
                var trainRoute1 = _environment.FindShortestRoute(startTrainStationNode,
                    transferTrainStationWaterwayNode, TrainDrivingFilter);
                trainRoutes.Add(trainRoute1);

                var trainRoute2 = _environment.FindShortestRoute(transferTrainStationWaterwayNode,
                    goalTrainStationNode,
                    TrainDrivingFilter);
                trainRoutes.Add(trainRoute2);
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
                                SpatialModalityType.TrainDriving);
                            var transferGoalNode = _environment.NearestNode(transferGoal.Position,
                                SpatialModalityType.TrainDriving);

                            var trainRoute1 = _environment.FindShortestRoute(startTrainStationNode, transferStartNode,
                                TrainDrivingFilter);
                            trainRoutes.Add(trainRoute1);

                            var trainRoute2 = _environment.FindShortestRoute(transferStartNode, transferGoalNode,
                                TrainDrivingFilter);
                            trainRoutes.Add(trainRoute2);

                            var trainRoute3 = _environment.FindShortestRoute(transferGoalNode, goalTrainStationNode,
                                TrainDrivingFilter);
                            trainRoutes.Add(trainRoute3);
                        }
                    }
                }
            }
        }

        return trainRoutes;

        bool TrainDrivingFilter(ISpatialEdge edge)
        {
            return edge.Modalities.Contains(SpatialModalityType.TrainDriving);
        }
    }
}