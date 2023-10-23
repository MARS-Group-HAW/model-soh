using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHDomain.Graph;
using SOHFerryModel.Station;

namespace SOHMultimodalModel.Routing;

public class WalkingFerryDrivingMultimodalRoute : MultimodalRoute
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly IFerryStationLayer _ferryStationLayer;
    private readonly Position _goal;
    private readonly Position _start;

    /// <summary>
    ///     Describes a multimodal route with walk, ferry drive, walk.
    /// </summary>
    /// <param name="environmentLayer">Contains the environment.</param>
    /// <param name="stationLayer">The station layer containing all ferry stations to route to.</param>
    /// <param name="start">Position where the route should start.</param>
    /// <param name="goal">Position where the route should end.</param>
    public WalkingFerryDrivingMultimodalRoute(ISpatialGraphLayer environmentLayer,
        IFerryStationLayer stationLayer, Position start, Position goal)
    {
        _start = start;
        _goal = goal;

        _ferryStationLayer = stationLayer;
        _environment = environmentLayer.Environment;

        var (startFerryStation, routeToFirstStation) = FindStartFerryStationAndWalkingRoute();
        var (goalFerryStation, routeToGoal) = FindGoalFerryStationAndFinalWalkingRoute();
        var ferryRoutes = FindFerryRoutes(startFerryStation, goalFerryStation);

        if (routeToFirstStation != null) Add(routeToFirstStation, ModalChoice.Walking);
        foreach (var ferryRoute in ferryRoutes) Add(ferryRoute, ModalChoice.Ferry);
        if (routeToGoal != null) Add(routeToGoal, ModalChoice.Walking);
    }

    private (FerryStation, Route) FindStartFerryStationAndWalkingRoute(HashSet<FerryStation> unreachable = null)
    {
        unreachable ??= new HashSet<FerryStation>();
        var ferryStation = _ferryStationLayer.Nearest(_start, station => !unreachable.Contains(station));
        if (ferryStation == null)
            throw new ApplicationException($"No reachable ferry station found for route from {_start} to {_goal}");

        var startNode = _environment.NearestNode(_start, SpatialModalityType.Walking);
        var ferryStationNode = _environment.NearestNode(ferryStation.Position, SpatialModalityType.Walking);
        if (startNode.Equals(ferryStationNode))
            return (ferryStation, null);

        var route = _environment.FindShortestRoute(startNode, ferryStationNode, WalkingFilter);
        if (route == null) // no walking route exists, ferry station is excluded from next search
        {
            unreachable.Add(ferryStation);
            return FindStartFerryStationAndWalkingRoute(unreachable);
        }

        // var distance = startNode.Position.DistanceInMTo(ferryStationNode.Position);
        // if (route.RouteLength > distance * 2)
        {
            var nextFerryStation = _ferryStationLayer.Nearest(_start,
                station => !unreachable.Contains(station) && station != ferryStation);
            if (nextFerryStation != null)
            {
                var nextFerryStationNode = _environment.NearestNode(nextFerryStation.Position);
                if (!startNode.Equals(nextFerryStationNode))
                {
                    var nextRoute = _environment.FindShortestRoute(startNode, nextFerryStationNode, WalkingFilter);
                    if (nextRoute?.RouteLength < route.RouteLength)
                        return (nextFerryStation, nextRoute);
                }
            }
        }

        return (ferryStation, route);
    }

    private (FerryStation, Route) FindGoalFerryStationAndFinalWalkingRoute(HashSet<FerryStation> unreachable = null)
    {
        unreachable ??= new HashSet<FerryStation>();
        var ferryStation = _ferryStationLayer.Nearest(_goal, station => !unreachable.Contains(station));
        if (ferryStation == null)
            throw new ApplicationException(
                $"No ferry route available within the spatial graph environment to reach goal station from {_start} to {_goal}");

        var ferryStationNode = _environment.NearestNode(ferryStation.Position, SpatialModalityType.Walking);
        var goalNode = _environment.NearestNode(_goal, SpatialModalityType.Walking);
        if (ferryStationNode.Equals(goalNode))
            return (ferryStation, null);

        var route = _environment.FindShortestRoute(ferryStationNode, goalNode, WalkingFilter);
        if (route != null)
        {
            var distance = goalNode.Position.DistanceInMTo(ferryStationNode.Position);
            if (route.RouteLength > distance * 2)
            {
                var nextFerryStation = _ferryStationLayer.Nearest(_goal,
                    station => !unreachable.Contains(station) && station != ferryStation);
                if (nextFerryStation != null)
                {
                    var nextFerryStationNode = _environment.NearestNode(nextFerryStation.Position);
                    if (!goalNode.Equals(nextFerryStationNode))
                    {
                        var nextRoute =
                            _environment.FindShortestRoute(nextFerryStationNode, goalNode, WalkingFilter);
                        if (nextRoute?.RouteLength < route.RouteLength) return (nextFerryStation, nextRoute);
                    }
                }
            }

            return (ferryStation, route);
        }

        unreachable.Add(ferryStation);
        return FindGoalFerryStationAndFinalWalkingRoute(unreachable);
    }

    private IEnumerable<Route> FindFerryRoutes(FerryStation startFerryStation, FerryStation goalFerryStation)
    {
        var ferryRoutes = new List<Route>();
        var startFerryStationNode =
            _environment.NearestNode(startFerryStation.Position, SpatialModalityType.ShipDriving);
        var goalFerryStationNode =
            _environment.NearestNode(goalFerryStation.Position, SpatialModalityType.ShipDriving);

        if (startFerryStationNode.Equals(goalFerryStationNode)) return ferryRoutes;

        var startLines = startFerryStation.Lines;
        var goalLines = goalFerryStation.Lines;

        bool ShipDrivingFilter(ISpatialEdge edge)
        {
            return edge.Modalities.Contains(SpatialModalityType.ShipDriving);
        }

        if (startLines.Intersect(goalLines).Any()) // find direct line
        {
            var ferryRoute =
                _environment.FindShortestRoute(startFerryStationNode, goalFerryStationNode, ShipDrivingFilter);
            ferryRoutes.Add(ferryRoute);
        }
        else // find line with transfer point
        {
            var transferPoint = _ferryStationLayer.Nearest(startFerryStation.Position,
                station => station.Lines.Intersect(startLines).Any() &&
                           station.Lines.Intersect(goalLines).Any());
            if (transferPoint != null) // single transfer point
            {
                var transferFerryStationWaterwayNode =
                    _environment.NearestNode(transferPoint.Position, SpatialModalityType.ShipDriving);
                var ferryRoute1 = _environment.FindShortestRoute(startFerryStationNode,
                    transferFerryStationWaterwayNode, ShipDrivingFilter);
                ferryRoutes.Add(ferryRoute1);

                var ferryRoute2 = _environment.FindShortestRoute(transferFerryStationWaterwayNode,
                    goalFerryStationNode,
                    ShipDrivingFilter);
                ferryRoutes.Add(ferryRoute2);
            }
            else // multiple transfer points
            {
                var transferPointsStart = _ferryStationLayer.Features.OfType<FerryStation>().Where(
                    station => station != startFerryStation && station.Lines.Intersect(startLines).Any() &&
                               station.Lines.Count > 1);
                var transferPointsGoal = _ferryStationLayer.Features.OfType<FerryStation>().Where(
                    station => station != goalFerryStation && station.Lines.Intersect(goalLines).Any() &&
                               station.Lines.Count > 1).ToList();

                foreach (var transferStart in transferPointsStart)
                foreach (var transferGoal in transferPointsGoal)
                    if (transferStart.Lines.Intersect(transferGoal.Lines).Any())
                    {
                        var transferStartNode = _environment.NearestNode(transferStart.Position,
                            SpatialModalityType.ShipDriving);
                        var transferGoalNode = _environment.NearestNode(transferGoal.Position,
                            SpatialModalityType.ShipDriving);

                        var ferryRoute1 = _environment.FindShortestRoute(startFerryStationNode, transferStartNode,
                            ShipDrivingFilter);
                        ferryRoutes.Add(ferryRoute1);

                        var ferryRoute2 = _environment.FindShortestRoute(transferStartNode, transferGoalNode,
                            ShipDrivingFilter);
                        ferryRoutes.Add(ferryRoute2);

                        var ferryRoute3 = _environment.FindShortestRoute(transferGoalNode, goalFerryStationNode,
                            ShipDrivingFilter);
                        ferryRoutes.Add(ferryRoute3);
                    }
            }
        }

        return ferryRoutes;
    }
}