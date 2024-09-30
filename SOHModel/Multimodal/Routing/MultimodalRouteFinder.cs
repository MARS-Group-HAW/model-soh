using Mars.Common.Core.Logging;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Bicycle.Rental;
using SOHModel.Bicycle.Steering;
using SOHModel.Bus.Steering;
using SOHModel.Car.Rental;
using SOHModel.Car.Steering;
using SOHModel.Domain.Graph;
using SOHModel.Ferry.Steering;
using SOHModel.Multimodal.Multimodal;
using SOHModel.Train.Steering;

namespace SOHModel.Multimodal.Routing;

/// <summary>
///     Single access point for finding a <code>MultimodalRoute</code> for given capabilities.
/// </summary>
public class MultimodalRouteFinder : IMultimodalRouteFinder
{
    private static readonly ILogger Logger = LoggerFactory.GetLogger(typeof(MultimodalRouteFinder));
    private readonly SpatialGraphMediatorLayer _environmentMediatorLayer;

    public MultimodalRouteFinder(SpatialGraphMediatorLayer environmentMediatorLayer)
    {
        _environmentMediatorLayer = environmentMediatorLayer;
    }

    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        ModalChoice modalChoice)
    {
        try
        {
            switch (modalChoice)
            {
                case ModalChoice.CarDriving:
                    return FindCarDrivingRoute(agent, start, goal);
                case ModalChoice.CarRentalDriving:
                    return FindCarRentalDrivingRoute(agent, start, goal);
                case ModalChoice.CyclingOwnBike:
                    return FindCyclingOwnBikeRoute(agent, start, goal);
                case ModalChoice.CyclingRentalBike:
                    return FindCyclingRentalBikeRoute(agent, start, goal);
                case ModalChoice.Ferry:
                    return FindFerryRoute(agent, start, goal);
                case ModalChoice.Bus:
                    return FindBusRoute(agent, start, goal);
                case ModalChoice.Train:
                    return FindTrainRoute(agent, start, goal);
            }
        }
        catch (ArgumentException exc)
        {
            Logger.LogInfo(exc.Message);
        }

        return FindWalkingRoute(start, goal);
    }

    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        IEnumerable<ModalChoice> capabilities)
    {
        var travelTime = double.MaxValue;
        MultimodalRoute? multimodalRoute = null;
        foreach (var capability in capabilities)
        {
            var route = Search(agent, start, goal, capability);
            var expectedTravelTime = route.ExpectedTravelTime(agent);
            if (expectedTravelTime < travelTime)
            {
                travelTime = expectedTravelTime;
                multimodalRoute = route;
            }
        }

        return multimodalRoute ?? Search(agent, start, goal, ModalChoice.Walking);
    }

    private MultimodalRoute FindCyclingOwnBikeRoute(IModalCapabilitiesAgent agent, Position start, Position goal)
    {
        if (agent is not IBicycleSteeringCapable cyclist)
            throw new ApplicationException($"The agent is not implementing '{typeof(IBicycleSteeringCapable)}'");
        if (cyclist.Bicycle == null)
            throw new ApplicationException("The agent is a cyclist but does not have an own bicycle");

        return new WalkingCyclingMultimodalRoute(_environmentMediatorLayer, cyclist.Bicycle, start, goal);
    }

    private MultimodalRoute FindCyclingRentalBikeRoute(IModalCapabilitiesAgent agent, Position start, Position goal)
    {
        if (agent is IBicycleSteeringAndRentalCapable { BicycleRentalLayer: not null } bicycleSteeringAndRentalCapable)
            return new WalkingCyclingMultimodalRoute(_environmentMediatorLayer,
                bicycleSteeringAndRentalCapable.BicycleRentalLayer, start, goal);


        throw new ApplicationException(
            $"Agent needs to be {nameof(IBicycleSteeringAndRentalCapable)} and has {nameof(BicycleRentalLayer)} to drive with rental bicycles.");
    }

    private MultimodalRoute FindCarDrivingRoute(IModalCapabilitiesAgent multimodalAgent, Position start,
        Position goal)
    {
        if (!_environmentMediatorLayer.Environment.Modalities.Contains(SpatialModalityType.Walking))
            throw new ApplicationException(
                $"The spatial graph environment has no lanes for '{SpatialModalityType.Walking}'");

        if (!_environmentMediatorLayer.Environment.Modalities.Contains(SpatialModalityType.CarDriving))
            throw new ApplicationException(
                $"The spatial graph environment has no lanes for '{SpatialModalityType.CarDriving}'");

        if (multimodalAgent is not ICarSteeringCapable driver)
            throw new ApplicationException($"The agent is not implementing '{typeof(ICarSteeringCapable)}'");
        if (driver.Car == null)
            throw new ApplicationException("The agent is a driver but does not have an own car");

        if (driver.CurrentlyCarDriving)
            return new WalkingDrivingMultimodalRoute(_environmentMediatorLayer, driver.Car, goal);

        if (driver.Car.CarParkingLayer == null)
            throw new ApplicationException(
                "The car layer of the car is null and have to be initialized first or prepared as a dependency");

        return new WalkingDrivingMultimodalRoute(_environmentMediatorLayer, driver.Car, start, goal);
    }

    private MultimodalRoute? FindCarRentalDrivingRoute(IModalCapabilitiesAgent multimodalAgent, Position start,
        Position goal)
    {
        if (multimodalAgent is ICarRentalCapable { CarRentalLayer: not null } capable)
            return new WalkingCarDrivingRentalMultimodalRoute(_environmentMediatorLayer, capable.CarRentalLayer,
                start, goal);

        return null;
    }

    private MultimodalRoute? FindFerryRoute(IModalCapabilitiesAgent agent, Position start, Position goal)
    {
        if (agent is IFerryPassenger { FerryStationLayer: not null } ferryPassenger)
            return new WalkingFerryDrivingMultimodalRoute(_environmentMediatorLayer,
                ferryPassenger.FerryStationLayer, start, goal);

        return null;
    }

    private MultimodalRoute? FindTrainRoute(IModalCapabilitiesAgent agent, Position start, Position goal)
    {
        if (agent is ITrainPassenger { TrainStationLayer: not null } trainPassenger)
            return new WalkingTrainDrivingMultimodalRoute(_environmentMediatorLayer,
                trainPassenger.TrainStationLayer, start, goal);

        return null;
    }
    
    private MultimodalRoute? FindBusRoute(IModalCapabilitiesAgent agent, Position start, Position goal)
    {
        if (agent is IBusPassenger { BusStationLayer: not null } trainPassenger)
        {
            return new WalkingBusDrivingMultimodalRoute(
                _environmentMediatorLayer, 
                trainPassenger.BusStationLayer, 
                start, goal);
        }

        return null;
    }

    private MultimodalRoute FindWalkingRoute(Position start, Position goal)
    {
        return new WalkingMultimodalRoute(_environmentMediatorLayer, start, goal);
    }
}