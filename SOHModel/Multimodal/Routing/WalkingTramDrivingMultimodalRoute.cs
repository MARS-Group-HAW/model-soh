using Mars.Interfaces.Environments;
using SOHModel.Domain.Graph;
using SOHModel.Tram.Station;

namespace SOHModel.Multimodal.Routing;

public class WalkingTramDrivingMultimodalRoute : MultimodalRoute
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly Position _goal;
    private readonly Position _start;
    private readonly ITramStationLayer _tramStationLayer;

    /// <summary>
    ///     Describes a multimodal route with walk, Tram drive, walk.
    /// </summary>
    /// <param name="environmentLayer">Contains the environment.</param>
    /// <param name="stationLayer">The station layer containing all Tram stations to route to.</param>
    /// <param name="start">Position where the route should start.</param>
    /// <param name="goal">Position where the route should end.</param>
    public WalkingTramDrivingMultimodalRoute(ISpatialGraphLayer environmentLayer,
        ITramStationLayer stationLayer, Position start, Position goal)
    {
        _start = start;
        _goal = goal;

        _tramStationLayer = stationLayer;
        _environment = environmentLayer.Environment;

        var (startTramStation, routeToFirstStation) = FindStartTramStationAndWalkingRoute();
        var (goalTramStation, routeToGoal) = FindGoalTramStationAndFinalWalkingRoute();
        var tramRoutes = FindTramRoutes(startTramStation, goalTramStation).ToList();

        if (tramRoutes.Count == 0)
            throw new ArgumentException("Could not find any tram route.");

        if (routeToFirstStation != null) Add(routeToFirstStation, ModalChoice.Walking);
        foreach (var tramRoute in tramRoutes) Add(tramRoute, ModalChoice.Train);
        if (routeToGoal != null) Add(routeToGoal, ModalChoice.Walking);
    }

    private (TramStation, Route) FindStartTramStationAndWalkingRoute(HashSet<TramStation> unreachable = null)
    {
        unreachable ??= new HashSet<TramStation>();
        var tramStation = _tramStationLayer.Nearest(_start, station => !unreachable.Contains(station));
        if (tramStation == null)
            throw new ApplicationException($"No reachable tram station found for route from {_start} to {_goal}");

        var startNode = _environment.NearestNode(_start, SpatialModalityType.Walking);
        var tramStationNode = _environment.NearestNode(tramStation.Position, SpatialModalityType.Walking);
        if (startNode.Equals(tramStationNode))
            return (tramStation, null);

        var route = _environment.FindShortestRoute(startNode, tramStationNode, WalkingFilter);
        if (route == null) // no walking route exists, tram station is excluded from next search
        {
            unreachable.Add(tramStation);
            return FindStartTramStationAndWalkingRoute(unreachable);
        }

        // var distance = startNode.Position.DistanceInMTo(tramStationNode.Position);
        // if (route.RouteLength > distance * 2)
        {
            var nextTramStation = _tramStationLayer.Nearest(_start,
                station => !unreachable.Contains(station) && station != tramStation);
            if (nextTramStation != null)
            {
                var nextTramStationNode = _environment.NearestNode(nextTramStation.Position);
                if (!startNode.Equals(nextTramStationNode))
                {
                    var nextRoute = _environment.FindShortestRoute(startNode, nextTramStationNode, WalkingFilter);
                    if (nextRoute?.RouteLength < route.RouteLength)
                        return (nextTramStation, nextRoute);
                }
            }
        }

        return (tramStation, route);
    }

    private (TramStation, Route) FindGoalTramStationAndFinalWalkingRoute(HashSet<TramStation> unreachable = null)
    {
        unreachable ??= new HashSet<TramStation>();
        var tramStation = _tramStationLayer.Nearest(_goal, station => !unreachable.Contains(station));
        if (tramStation == null)
            throw new ApplicationException(
                $"No tram route available within the spatial graph environment to reach goal station from {_start} to {_goal}");

        var tramStationNode = _environment.NearestNode(tramStation.Position, SpatialModalityType.Walking);
        var goalNode = _environment.NearestNode(_goal, SpatialModalityType.Walking);
        if (tramStationNode.Equals(goalNode))
            return (tramStation, null);

        var route = _environment.FindShortestRoute(tramStationNode, goalNode, WalkingFilter);
        if (route != null)
        {
            var distance = goalNode.Position.DistanceInMTo(tramStationNode.Position);
            if (route.RouteLength > distance * 2)
            {
                var nextTramStation = _tramStationLayer.Nearest(_goal,
                    station => !unreachable.Contains(station) && station != tramStation);
                if (nextTramStation != null)
                {
                    var nextTramStationNode = _environment.NearestNode(nextTramStation.Position);
                    if (!goalNode.Equals(nextTramStationNode))
                    {
                        var nextRoute =
                            _environment.FindShortestRoute(nextTramStationNode, goalNode, WalkingFilter);
                        if (nextRoute?.RouteLength < route.RouteLength) return (nextTramStation, nextRoute);
                    }
                }
            }

            return (tramStation, route);
        }

        unreachable.Add(tramStation);
        return FindGoalTramStationAndFinalWalkingRoute(unreachable);
    }

    private IEnumerable<Route> FindTramRoutes(TramStation startTramStation, TramStation goalTramStation)
    {
        var tramRoutes = new List<Route>();
        var startTramStationNode =
            _environment.NearestNode(startTramStation.Position, SpatialModalityType.TrainDriving);
        var goalTramStationNode =
            _environment.NearestNode(goalTramStation.Position, SpatialModalityType.TrainDriving);

        if (startTramStationNode.Equals(goalTramStationNode)) return tramRoutes;

        var startLines = startTramStation.Lines;
        var goalLines = goalTramStation.Lines;

        bool TramDrivingFilter(ISpatialEdge edge)
        {
            return edge.Modalities.Contains(SpatialModalityType.TrainDriving);
        }

        if (startLines.Intersect(goalLines).Any()) // find direct line
        {
            var tramRoute =
                _environment.FindShortestRoute(startTramStationNode, goalTramStationNode, TramDrivingFilter);
            tramRoutes.Add(tramRoute);
        }
        else // find line with transfer point
        {
            var transferPoint = _tramStationLayer.Nearest(startTramStation.Position,
                station => station.Lines.Intersect(startLines).Any() &&
                           station.Lines.Intersect(goalLines).Any());
            if (transferPoint != null) // single transfer point
            {
                var transferTramStationNode =
                    _environment.NearestNode(transferPoint.Position, SpatialModalityType.TrainDriving);
                var tramRoute1 = _environment.FindShortestRoute(startTramStationNode,
                    transferTramStationNode, TramDrivingFilter);
                tramRoutes.Add(tramRoute1);

                var tramRoute2 = _environment.FindShortestRoute(transferTramStationNode,
                    goalTramStationNode, TramDrivingFilter);
                tramRoutes.Add(tramRoute2);
            }
            else // multiple transfer points
            {
                var transferPointsStart = _tramStationLayer.Features.OfType<TramStation>().Where(station =>
                    station != startTramStation && station.Lines.Intersect(startLines).Any() &&
                    station.Lines.Count > 1);
                var transferPointsGoal = _tramStationLayer.Features.OfType<TramStation>().Where(station =>
                    station != goalTramStation && station.Lines.Intersect(goalLines).Any() &&
                    station.Lines.Count > 1).ToList();

                foreach (var transferStart in transferPointsStart)
                foreach (var transferGoal in transferPointsGoal)
                    if (transferStart.Lines.Intersect(transferGoal.Lines).Any())
                    {
                        var transferStartNode = _environment.NearestNode(transferStart.Position,
                            SpatialModalityType.TrainDriving);
                        var transferGoalNode = _environment.NearestNode(transferGoal.Position,
                            SpatialModalityType.TrainDriving);

                        var tramRoute1 = _environment.FindShortestRoute(startTramStationNode, transferStartNode,
                            TramDrivingFilter);
                        tramRoutes.Add(tramRoute1);

                        var tramRoute2 = _environment.FindShortestRoute(transferStartNode, transferGoalNode,
                            TramDrivingFilter);
                        tramRoutes.Add(tramRoute2);

                        var tramRoute3 = _environment.FindShortestRoute(transferGoalNode, goalTramStationNode,
                            TramDrivingFilter);
                        tramRoutes.Add(tramRoute3);
                    }
            }
        }

        return tramRoutes;
    }
}