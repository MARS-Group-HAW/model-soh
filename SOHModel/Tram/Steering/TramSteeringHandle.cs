namespace SOHModel.Tram.Steering;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;
using Model;


public class TramSteeringHandle : VehicleSteeringHandle<ITramSteeringCapable, IPassengerCapable,
    TramSteeringHandle, TramPassengerHandle>
{
    private readonly ITramSteeringCapable _driver;

    public TramSteeringHandle(ISpatialGraphEnvironment environment, ITramSteeringCapable driver,
        Vehicle<ITramSteeringCapable, IPassengerCapable, TramSteeringHandle,
            TramPassengerHandle> vehicle, double standardSpeedLimit = 6.1722222)
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

        if (_driver is TramDriver tramDriver)
        {
            var tripTime = tramDriver.CurrentTramRouteEntry.Minutes * 60d -
                           tramDriver.MinimumBoardingTimeInSeconds * 2;
            var distance = Route.RouteLength / tripTime;

            if (distance > Route.RemainingRouteDistanceToGoal) // Make last step to goal
                return Route.RemainingRouteDistanceToGoal;
            return distance;
        }

        return base.CalculateDrivingDistance(biggestDeceleration);
    }
}
