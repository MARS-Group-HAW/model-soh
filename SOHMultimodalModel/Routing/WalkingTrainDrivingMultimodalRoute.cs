using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHDomain.Graph;
using SOHTrainModel.Station;

namespace SOHMultimodalModel.Routing;

public class WalkingTrainDrivingMultimodalRoute : MultimodalRoute
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly Position _goal;
    private readonly Position _start;
    private readonly ITrainStationLayer _trainStationLayer;

    /// <summary>
    ///     Describes a multimodal route with walk, Train drive, walk.
    /// </summary>
    /// <param name="environmentLayer">Contains the environment.</param>
    /// <param name="stationLayer">The station layer containing all Train stations to route to.</param>
    /// <param name="start">Position where the route should start.</param>
    /// <param name="goal">Position where the route should end.</param>
    public WalkingTrainDrivingMultimodalRoute(ISpatialGraphLayer environmentLayer,
        ITrainStationLayer stationLayer, Position start, Position goal)
    {
        _start = start;
        _goal = goal;

        _trainStationLayer = stationLayer;
        _environment = environmentLayer.Environment;

        var (startTrainStation, routeToFirstStation) = FindStartTrainStationAndWalkingRoute();
        var (goalTrainStation, routeToGoal) = FindGoalTrainStationAndFinalWalkingRoute();
        var trainRoutes = FindTrainRoutes(startTrainStation, goalTrainStation).ToList();

        if (trainRoutes.Count == 0)
            throw new ArgumentException("Could not find any train route.");

        if (routeToFirstStation != null) Add(routeToFirstStation, ModalChoice.Walking);
        foreach (var trainRoute in trainRoutes) Add(trainRoute, ModalChoice.Train);
        if (routeToGoal != null) Add(routeToGoal, ModalChoice.Walking);
    }

    private (TrainStation, Route) FindStartTrainStationAndWalkingRoute(HashSet<TrainStation> unreachable = null)
    {
        unreachable ??= new HashSet<TrainStation>();
        var trainStation = _trainStationLayer.Nearest(_start, station => !unreachable.Contains(station));
        if (trainStation == null)
            throw new ApplicationException($"No reachable Train station found for route from {_start} to {_goal}");

        var startNode = _environment.NearestNode(_start, SpatialModalityType.Walking);
        var trainStationNode = _environment.NearestNode(trainStation.Position, SpatialModalityType.Walking);
        if (startNode.Equals(trainStationNode))
            return (trainStation, null);

        var route = _environment.FindShortestRoute(startNode, trainStationNode, WalkingFilter);
        if (route == null) // no walking route exists, Train station is excluded from next search
        {
            unreachable.Add(trainStation);
            return FindStartTrainStationAndWalkingRoute(unreachable);
        }

        // var distance = startNode.Position.DistanceInMTo(TrainStationNode.Position);
        // if (route.RouteLength > distance * 2)
        {
            var nextTrainStation = _trainStationLayer.Nearest(_start,
                station => !unreachable.Contains(station) && station != trainStation);
            if (nextTrainStation != null)
            {
                var nextTrainStationNode = _environment.NearestNode(nextTrainStation.Position);
                if (!startNode.Equals(nextTrainStationNode))
                {
                    var nextRoute = _environment.FindShortestRoute(startNode, nextTrainStationNode, WalkingFilter);
                    if (nextRoute?.RouteLength < route.RouteLength)
                        return (nextTrainStation, nextRoute);
                }
            }
        }

        return (trainStation, route);
    }

    private (TrainStation, Route) FindGoalTrainStationAndFinalWalkingRoute(HashSet<TrainStation> unreachable = null)
    {
        unreachable ??= new HashSet<TrainStation>();
        var trainStation = _trainStationLayer.Nearest(_goal, station => !unreachable.Contains(station));
        if (trainStation == null)
            throw new ApplicationException(
                $"No Train route available within the spatial graph environment to reach goal station from {_start} to {_goal}");

        var trainStationNode = _environment.NearestNode(trainStation.Position, SpatialModalityType.Walking);
        var goalNode = _environment.NearestNode(_goal, SpatialModalityType.Walking);
        if (trainStationNode.Equals(goalNode))
            return (trainStation, null);

        var route = _environment.FindShortestRoute(trainStationNode, goalNode, WalkingFilter);
        if (route != null)
        {
            var distance = goalNode.Position.DistanceInMTo(trainStationNode.Position);
            if (route.RouteLength > distance * 2)
            {
                var nextTrainStation = _trainStationLayer.Nearest(_goal,
                    station => !unreachable.Contains(station) && station != trainStation);
                if (nextTrainStation != null)
                {
                    var nextTrainStationNode = _environment.NearestNode(nextTrainStation.Position);
                    if (!goalNode.Equals(nextTrainStationNode))
                    {
                        var nextRoute =
                            _environment.FindShortestRoute(nextTrainStationNode, goalNode, WalkingFilter);
                        if (nextRoute?.RouteLength < route.RouteLength) return (nextTrainStation, nextRoute);
                    }
                }
            }

            return (trainStation, route);
        }

        unreachable.Add(trainStation);
        return FindGoalTrainStationAndFinalWalkingRoute(unreachable);
    }

    private IEnumerable<Route> FindTrainRoutes(TrainStation startTrainStation, TrainStation goalTrainStation)
    {
        var trainRoutes = new List<Route>();
        var startTrainStationNode =
            _environment.NearestNode(startTrainStation.Position, SpatialModalityType.TrainDriving);
        var goalTrainStationNode =
            _environment.NearestNode(goalTrainStation.Position, SpatialModalityType.TrainDriving);

        if (startTrainStationNode.Equals(goalTrainStationNode)) return trainRoutes;

        var startLines = startTrainStation.Lines;
        var goalLines = goalTrainStation.Lines;

        bool TrainDrivingFilter(ISpatialEdge edge)
        {
            return edge.Modalities.Contains(SpatialModalityType.TrainDriving);
        }

        if (startLines.Intersect(goalLines).Any()) // find direct line
        {
            var trainRoute =
                _environment.FindShortestRoute(startTrainStationNode, goalTrainStationNode, TrainDrivingFilter);
            trainRoutes.Add(trainRoute);
        }
        else // find line with transfer point
        {
            var transferPoint = _trainStationLayer.Nearest(startTrainStation.Position,
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
                var transferPointsStart = _trainStationLayer.Features.OfType<TrainStation>().Where(
                    station => station != startTrainStation && station.Lines.Intersect(startLines).Any() &&
                               station.Lines.Count > 1);
                var transferPointsGoal = _trainStationLayer.Features.OfType<TrainStation>().Where(
                    station => station != goalTrainStation && station.Lines.Intersect(goalLines).Any() &&
                               station.Lines.Count > 1).ToList();


                foreach (var transferStart in transferPointsStart)
                foreach (var transferGoal in transferPointsGoal)
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

        return trainRoutes;
    }
}