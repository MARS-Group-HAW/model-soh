using System;
using System.Linq;
using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;
using SOHDomain.Graph;

namespace SOHMultimodalModel.Routing;

/// <summary>
///     Provides the possibility to find gateway points (exit the graph or enter the graph) for POIs that are outside of an
///     environment.
/// </summary>
public class GatewayLayer : VectorLayer<GatewayPoint>
{
    private readonly ISpatialGraphEnvironment _environment;

    public GatewayLayer(ISpatialGraphEnvironment environment = null)
    {
        _environment = environment;
    }

    public ISpatialGraphLayer GraphLayer { get; set; }

    private ISpatialGraphEnvironment Environment => _environment ?? GraphLayer.Environment;

    /// <summary>
    ///     Validates given start and goal position and returns a gateway point instead of the position if necessary.
    /// </summary>
    /// <param name="start">Position to start from.</param>
    /// <param name="goal">Position to reach.</param>
    /// <returns>A start and a goal position that is located within the environment and may function as gateway point.</returns>
    public (Position, Position) Validate(Position start, Position goal)
    {
        var box = Environment.BoundingBox;
        var startInside = box.Contains(start.ToCoordinate());
        var goalInside = box.Contains(goal.ToCoordinate());

        if (startInside)
            return goalInside ? (start, goal) : (start, FindGatewayPoint(start, goal));
        return goalInside ? (FindGatewayPoint(goal, start), goal) : (start, goal);
    }

    private Position FindGatewayPoint(Position start, Position goal)
    {
        var nearestPositionWithinEnv = Environment.NearestNode(goal).Position;
        if (WithinWalkingDistance(goal.DistanceInKmTo(nearestPositionWithinEnv))) return nearestPositionWithinEnv;

        if (Features == null || !Features.Any())
            throw new ApplicationException("GatewayLayer has no valid features. Check input vector file!");

        var gatewayPoint = Nearest(start.PositionArray);
        var gateWayPosition = gatewayPoint?.Position;
        return gateWayPosition == null ? null : Environment.NearestNode(gateWayPosition).Position;
    }

    private static bool WithinWalkingDistance(double distanceInKm)
    {
        return distanceInKm < 1;
    }
}