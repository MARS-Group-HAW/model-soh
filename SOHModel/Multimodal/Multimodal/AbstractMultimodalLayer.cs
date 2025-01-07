using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Bicycle.Parking;
using SOHModel.Bicycle.Rental;
using SOHModel.Bicycle.Steering;
using SOHModel.Bus.Station;
using SOHModel.Car.Parking;
using SOHModel.Car.Rental;
using SOHModel.Car.Steering;
using SOHModel.Domain.Graph;
using SOHModel.Ferry.Station;
using SOHModel.Multimodal.Routing;
using SOHModel.Train.Station;

namespace SOHModel.Multimodal.Multimodal;

/// <summary>
///     This class provides an abstract implementation for the <see cref="IMultimodalLayer" />.
/// </summary>
public abstract class AbstractMultimodalLayer : AbstractLayer, IMultimodalLayer, IModalChoiceResolver
{
    private IMultimodalRouteFinder? _routeFinder;

    /// <summary>
    ///     Provides the possibility to search multimodal routes, respecting
    ///     multiple different modalities given by layer.
    /// </summary>
    public IMultimodalRouteFinder RouteFinder
    {
        get
        {
            if (_routeFinder == null)
            {
                _routeFinder = new MultimodalRouteFinder(SpatialGraphMediatorLayer);
            }

            return _routeFinder;
        }
    }

    /// <summary>
    ///     Holds all environment layers for any movement.
    /// </summary>
    [PropertyDescription]
    public SpatialGraphMediatorLayer SpatialGraphMediatorLayer { get; set; } = default!;

    /// <summary>
    ///     Gets the <see cref="FerryStationLayer" /> holding all stations
    ///     where the <see cref="ModalChoice.Ferry" /> is available.
    /// </summary>
    [PropertyDescription]
    public FerryStationLayer FerryStationLayer { get; set; } = default!;

    /// <summary>
    ///     Gets the <see cref="TrainStationLayer" /> holding all stations
    ///     where the <see cref="ModalChoice.Train" /> is available.
    /// </summary>
    [PropertyDescription]
    public TrainStationLayer? TrainStationLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="BusStationLayer" /> holding all stations
    ///     where the <see cref="ModalChoice.Bus" /> is available.
    /// </summary>
    [PropertyDescription]
    public BusStationLayer? BusStationLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="BicycleParkingLayer" /> holding all parking lots
    ///     where the <see cref="ModalChoice.CyclingOwnBike" /> can be parked.
    /// </summary>
    [PropertyDescription]
    public BicycleParkingLayer? BicycleParkingLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="BicycleRentalLayer" /> holding all rental stations
    ///     where the <see cref="ModalChoice.CyclingRentalBike" /> is available.
    /// </summary>
    [PropertyDescription]
    public BicycleRentalLayer? BicycleRentalLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="CarParkingLayer" /> holding all parking spaces
    ///     where the <see cref="ModalChoice.CarDriving" /> may start or end its drive.
    /// </summary>
    [PropertyDescription]
    public CarParkingLayer? CarParkingLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="CarRentalLayer" /> holding all rental stations
    ///     where the <see cref="ModalChoice.CarRentalDriving" /> is available.
    /// </summary>
    [PropertyDescription]
    public CarRentalLayer? CarRentalLayer { get; set; }

    public IEnumerable<ModalChoice> Provides(IModalCapabilitiesAgent agent, ISpatialNode source)
    {
        IEnumerable<ModalChoice> result = Array.Empty<ModalChoice>();
        foreach (var modalChoice in agent.ModalChoices)
        {
            var environment = SpatialGraphMediatorLayer.Environment;
            switch (modalChoice)
            {
                case ModalChoice.Walking:
                    result = result.Append(modalChoice);
                    break;
                case ModalChoice.CyclingOwnBike:
                    if (agent is IBicycleSteeringCapable { Bicycle : not null } cyclist)
                    {
                        var parkingLot = cyclist.Bicycle.BicycleParkingLot;
                        var bicyclePosition = parkingLot != null ? parkingLot.Position : cyclist.Bicycle.Position;
                        var nearestNode = environment.NearestNode(bicyclePosition);
                        if (source.Equals(nearestNode)) result = result.Append(modalChoice);
                    }

                    break;
                case ModalChoice.CyclingRentalBike:
                    result = result.Concat(ResolveModalChoice(source, modalChoice, BicycleRentalLayer));
                    break;
                case ModalChoice.CarDriving:
                    if (agent is ICarSteeringCapable { Car : not null } driver)
                    {
                        var carParkingSpace = driver.Car.CarParkingSpace;
                        if (carParkingSpace == null) break;
                        var nearestNode = environment.NearestNode(carParkingSpace.Position);
                        if (source.Equals(nearestNode)) result = result.Append(modalChoice);
                    }

                    break;
                case ModalChoice.Ferry:
                    result = result.Concat(ResolveModalChoice(source, modalChoice, FerryStationLayer));
                    break;
                case ModalChoice.Train:
                    result = result.Concat(ResolveModalChoice(source, modalChoice, TrainStationLayer));
                    break;
                case ModalChoice.Bus:
                    result = result.Concat(ResolveModalChoice(source, modalChoice, BusStationLayer));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return result;
    }

    public bool Consumes(ModalChoice modalChoice, ISpatialNode source)
    {
        return modalChoice switch
        {
            ModalChoice.Walking => true,
            ModalChoice.CyclingOwnBike => true,
            ModalChoice.CyclingRentalBike => ConsumeModalChoice(source, BicycleRentalLayer),
            ModalChoice.CarDriving => ConsumeModalChoice(source, CarParkingLayer),
            ModalChoice.Ferry => ConsumeModalChoice(source, FerryStationLayer),
            ModalChoice.Train => ConsumeModalChoice(source, TrainStationLayer),
            ModalChoice.Bus => ConsumeModalChoice(source, BusStationLayer),
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        ModalChoice modalChoice, Position busStop = null)
    {
        return RouteFinder.Search(agent, start, goal, modalChoice, busStop);
    }

    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        IEnumerable<ModalChoice> capabilities)
    {
        return RouteFinder.Search(agent, start, goal, capabilities);
    }

    private IEnumerable<ModalChoice> ResolveModalChoice<T>(
        ISpatialNode source, 
        ModalChoice modalChoice, 
        IVectorLayer<T>? vectorLayer) where T : IVectorFeature
    {
        if (vectorLayer == null) yield break;

        var feature = vectorLayer.Nearest(source.Position.PositionArray);
        if (feature == null) yield break;

        var centroid = feature.VectorStructured.Geometry.Centroid;
        var featurePosition = Position.CreateGeoPosition(centroid.X, centroid.Y);
        var nearestNode =
            SpatialGraphMediatorLayer.Environment.NearestNode(featurePosition);
        if (source.Equals(nearestNode))
            yield return modalChoice;
    }

    private bool ConsumeModalChoice<T>(
        ISpatialNode source, 
        IVectorLayer<T>? vectorLayer) where T : IVectorFeature
    {
        if (vectorLayer == null) return false;

        var feature = vectorLayer.Nearest(source.Position.PositionArray);
        if (feature == null) return false;

        var centroid = feature.VectorStructured.Geometry.Centroid;
        var featurePosition = Position.CreateGeoPosition(centroid.X, centroid.Y);
        var nearestNode = SpatialGraphMediatorLayer.Environment.NearestNode(featurePosition);

        return source.Equals(nearestNode);
    }
}