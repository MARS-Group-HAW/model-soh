using System;
using System.Linq;
using Mars.Interfaces.Environments;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;
using SOHTrainModel.Model;

namespace SOHTrainModel.Steering;

public class TrainSteeringHandle : VehicleSteeringHandle<ITrainSteeringCapable, IPassengerCapable,
    TrainSteeringHandle, TrainPassengerHandle>
{
    private readonly ITrainSteeringCapable _driver;

    public TrainSteeringHandle(ISpatialGraphEnvironment environment, ITrainSteeringCapable driver,
        Vehicle<ITrainSteeringCapable, IPassengerCapable, TrainSteeringHandle,
            TrainPassengerHandle> vehicle, double standardSpeedLimit = 6.1722222)
        : base(environment, vehicle, standardSpeedLimit)
    {
        _driver = driver;
    }

    protected override double HandleIntersectionAhead(SpatialGraphExploreResult exploreResult,
        double biggestDeceleration)
    {
        var exploreResults = exploreResult.EdgeExplores;

        if (exploreResults.Count == 1)
        {
            var edgeExploreResult = exploreResults.First();
            var distanceToStation = edgeExploreResult.IntersectionDistance;

            if (distanceToStation < UrbanSafetyDistanceInM)
            {
                var speedChange = CalculateSpeedChange(Vehicle.Velocity, SpeedLimit,
                    distanceToStation, 0, 0);
                return Math.Min(speedChange, biggestDeceleration);
            }
        }

        return biggestDeceleration;
    }

    protected override double CalculateDrivingDistance(double biggestDeceleration)
    {
        if (Route.RemainingRouteDistanceToGoal < 3) // Make last step to goal
            return Route.RemainingRouteDistanceToGoal;
        if (biggestDeceleration < MaximalDeceleration) // Was speed change set? then use it
            return Vehicle.Velocity + biggestDeceleration;

        if (_driver is TrainDriver trainDriver)
        {
            var tripTime = trainDriver.CurrentTrainRouteEntry.Minutes * 60d -
                           trainDriver.MinimumBoardingTimeInSeconds * 2;
            var distance = Route.RouteLength / tripTime;

            if (distance > Route.RemainingRouteDistanceToGoal) // Make last step to goal
                return Route.RemainingRouteDistanceToGoal;
            return distance;
        }

        return base.CalculateDrivingDistance(biggestDeceleration);
    }
}