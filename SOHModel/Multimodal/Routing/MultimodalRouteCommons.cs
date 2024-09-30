using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.Multimodal.Routing;

/// <summary>
///     Provides <code>MultimodalRoute</code> util functionality.
/// </summary>
public static class MultimodalRouteCommons
{
    /// <summary>
    ///     Provides a key figure that indicates the expected travel time for the whole multimodal route with given agent.
    /// </summary>
    /// <param name="multimodalRoute">Holds the full route with its different modalities.</param>
    /// <param name="agent">Provides information about the walking velocity.</param>
    /// <returns>The calculated travel time (rounded).</returns>
    public static int ExpectedTravelTime(this MultimodalRoute multimodalRoute, IModalCapabilitiesAgent agent = null)
    {
        var travelTime = 0d;

        var stops = multimodalRoute?.Stops;
        if (stops == null || !stops.Any() || !stops.First().Route.Any()) return int.MaxValue;

        foreach (var routeStop in stops)
            switch (routeStop.ModalChoice)
            {
                case ModalChoice.Walking:
                    var pedestrian = agent as IWalkingCapable;
                    var walkingSpeed = pedestrian?.PreferredSpeed > 0 ? pedestrian.PreferredSpeed : 5 / 3.6;
                    travelTime += routeStop.Route.RouteLength / walkingSpeed;
                    break;
                case ModalChoice.CarDriving:
                case ModalChoice.CoDriving:
                    const double intersectionPenalty = 10;
                    travelTime += (from stop in routeStop.Route
                        let length = stop.Edge.Length
                        let maxSpeedEdge = Math.Abs(stop.Edge.MaxSpeed)
                        let maxSpeed = maxSpeedEdge > 0.01 ? maxSpeedEdge : 30 / 3.6
                        select length / maxSpeed + intersectionPenalty).Sum();

                    break;
                case ModalChoice.CyclingOwnBike:
                case ModalChoice.CyclingRentalBike:
                    travelTime += routeStop.Route.RouteLength / (20 / 3.6);
                    break;
            }

        return (int)Math.Ceiling(travelTime);
    }

    /// <summary>
    ///     Provides the distances between the switching points in a multimodal route.
    /// </summary>
    /// <param name="multimodalRoute">That is used.</param>
    /// <returns></returns>
    public static List<double> GiveDistanceOfSwitchPoints(MultimodalRoute multimodalRoute)
    {
        var distances = new List<double>();
        if (multimodalRoute.Stops.Count() < 2) return distances;

        Position endPointOfPrevious = null;
        foreach (var stop in multimodalRoute.Stops)
        {
            if (endPointOfPrevious != null)
            {
                var startPointOfNext = stop.Route.Stops.First().Edge.From.Position;
                var distance = startPointOfNext.DistanceInMTo(endPointOfPrevious);
                distances.Add(distance);
            }

            endPointOfPrevious = stop.Route.Stops.Last().Edge.To.Position;
        }

        return distances;
    }
}