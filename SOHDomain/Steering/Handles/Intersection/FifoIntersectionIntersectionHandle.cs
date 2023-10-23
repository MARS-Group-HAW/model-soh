using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHDomain.Model;
using SOHDomain.Steering.Acceleration;
using SOHDomain.Steering.Capables;

namespace SOHDomain.Steering.Handles.Intersection;

/// <summary>
///     First come first serve principle for intersection right of way.
/// </summary>
/// <typeparam name="TSteeringCapable"></typeparam>
/// <typeparam name="TPassengerCapable"></typeparam>
/// <typeparam name="TSteeringHandle"></typeparam>
/// <typeparam name="TPassengerHandle"></typeparam>
public class FifoIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> :
    AbstractIntersectionHandle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>
    where TPassengerHandle : IPassengerHandle
    where TSteeringHandle : ISteeringHandle
    where TPassengerCapable : IPassengerCapable
    where TSteeringCapable : ISteeringCapable
{
    public FifoIntersectionHandle(
        Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        IVehicleAccelerator vehicleAccelerator)
        : base(vehicle, vehicleAccelerator)
    {
    }

    public override double Evaluate(EdgeExploreResult edgeExploreResult, DirectionType vehicleDirection)
    {
        double biggestDeceleration = 1000;
        var orderOfArrival = new List<ISpatialGraphEntity>();

        // Establish arrival order
        if (edgeExploreResult.IntersectionDistance < 20 && orderOfArrival.Count == 0)
        {
            var incomingCars = CollectIncomingEntities(edgeExploreResult.Edge.To);
            var arrivalOrder = incomingCars.OrderBy(kvp => kvp.CurrentEdge.Length - kvp.PositionOnCurrentEdge);
            orderOfArrival.AddRange(arrivalOrder);
        }

        // Come to a full stop
        if (Vehicle.Velocity > 0 && Vehicle.RemainingDistanceOnEdge > 2)
        {
            var acc = VehicleAccelerator.CalculateSpeedChange(Vehicle.Velocity, edgeExploreResult.Edge.MaxSpeed,
                edgeExploreResult.IntersectionDistance, 0);

            if (acc < biggestDeceleration) biggestDeceleration = acc;
        }

        // See if its my turn to go
        else
        {
            // Filter incoming cars from all edges which are less than 1m before intersection
            var incomingCars = CollectIncomingEntities(edgeExploreResult.Edge.To).Where(entity =>
                Math.Abs(entity.PositionOnCurrentEdge - entity.CurrentEdge.Length) < 10);


            // orderOfArrival: 
            // - alle einfahrenden autos
            // - sortiert nach distanz zur kreuzung
            // - beginnend mit der geringsten disanz

            // entferne alle atuso die weiter weg sind als 1m entfernen wir
            // wir bleiben erhalten

            var remove = orderOfArrival.Where(entity => !incomingCars.Contains(entity) && entity != Vehicle)
                .ToList();

            foreach (var guid in remove) orderOfArrival.Remove(guid);

            // if no car is in the list, add ourself
            // if list is empty the following FirstOrDefault() would always trigger 
            // ththe if clause and all cars would stop forever
            if (orderOfArrival.Count == 0) orderOfArrival.Add(Vehicle);

            if (orderOfArrival.FirstOrDefault() != Vehicle && biggestDeceleration > 0) biggestDeceleration = 0;
        }

        return biggestDeceleration;
    }
}