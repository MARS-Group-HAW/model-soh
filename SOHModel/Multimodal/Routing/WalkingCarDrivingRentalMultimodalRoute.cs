using Mars.Interfaces.Environments;
using SOHModel.Car.Rental;
using SOHModel.Domain.Graph;

namespace SOHModel.Multimodal.Routing;

public class WalkingCarDrivingRentalMultimodalRoute : MultimodalRoute
{
    /// <summary>
    ///     Driver finds rental car, walks there and drives to destination then.
    /// </summary>
    /// <param name="envLayer">Provides access to spatial structures.</param>
    /// <param name="carRentalLayer">That provides access to rental cars.</param>
    /// <param name="start">To start.</param>
    /// <param name="goal">To reach.</param>
    public WalkingCarDrivingRentalMultimodalRoute(
        ISpatialGraphLayer envLayer,
        ICarRentalLayer carRentalLayer, 
        Position start, Position goal)
    {
        var env = envLayer.Environment;
        var rentalCar = carRentalLayer.Nearest(start);
        if (rentalCar == null)
            throw new ArgumentException("No rental car found.");

        var currentSidewalkNode = env.NearestNode(start, SpatialModalityType.Walking);
        var rentalCarSidewalkNode = env.NearestNode(rentalCar.Position, SpatialModalityType.Walking);
        if (!currentSidewalkNode.Equals(rentalCarSidewalkNode))
        {
            var route = env.FindShortestRoute(currentSidewalkNode, rentalCarSidewalkNode, WalkingFilter);
            if (route != null) Add(route, ModalChoice.Walking);
        }

        var rentalCarStreetNode = env.NearestNode(rentalCar.Position, SpatialModalityType.CarDriving);
        var goalStreetNode = env.NearestNode(goal, SpatialModalityType.CarDriving);
        if (!rentalCarStreetNode.Equals(goalStreetNode))
        {
            var routeWithCar = FindDrivingRoute(env, rentalCarStreetNode, goalStreetNode);
            Add(routeWithCar, ModalChoice.CarRentalDriving);
        }

        var parkingGoalSidewalkNode = env.NearestNode(goalStreetNode.Position, SpatialModalityType.Walking);
        var nodeSidewalkGoal = env.NearestNode(goal, SpatialModalityType.Walking);
        if (!nodeSidewalkGoal.Equals(parkingGoalSidewalkNode))
        {
            var routeToGoal = env.FindShortestRoute(parkingGoalSidewalkNode, nodeSidewalkGoal, WalkingFilter);
            Add(routeToGoal, ModalChoice.Walking);
        }
    }

    private static Route FindDrivingRoute(ISpatialGraphEnvironment environment, ISpatialNode startNode,
        ISpatialNode goalNode)
    {
        var route = environment.FindFastestRoute(startNode, goalNode,
            edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));
        if (route != null) return route;

        const int hops = 3;
        foreach (var tempStartNode in environment.NearestNodes(startNode.Position, double.MaxValue, hops))
        foreach (var tempGoalNode in environment.NearestNodes(goalNode.Position, double.MaxValue, hops))
        {
            route = environment.FindFastestRoute(tempStartNode, tempGoalNode,
                edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));
            if (route != null) return route;
        }

        throw new ArgumentException(
            $"Could not find any route for car from {startNode.Position} to {goalNode.Position}");
    }
}