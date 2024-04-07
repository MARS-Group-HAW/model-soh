using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;
using SOHModel.Ferry.Model;

namespace SOHModel.Ferry.Steering;

public class FerrySteeringHandle : VehicleSteeringHandle<IFerrySteeringCapable, IPassengerCapable,
    FerrySteeringHandle,
    FerryPassengerHandle>
{
    private readonly IFerrySteeringCapable _driver;

    public FerrySteeringHandle(ISpatialGraphEnvironment environment, IFerrySteeringCapable driver,
        Vehicle<IFerrySteeringCapable, IPassengerCapable, FerrySteeringHandle,
            FerryPassengerHandle> vehicle, double standardSpeedLimit = 6.1722222)
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

        if (_driver is FerryDriver ferryDriver)
        {
            var tripTime = ferryDriver.CurrentFerryRouteEntry.Minutes * 60d -
                           ferryDriver.MinimumBoardingTimeInSeconds * 2;
            var distance = Route.RouteLength / tripTime;

            if (distance > Route.RemainingRouteDistanceToGoal) // Make last step to goal
                return Route.RemainingRouteDistanceToGoal;
            return distance;
        }

        return base.CalculateDrivingDistance(biggestDeceleration);
    }
}