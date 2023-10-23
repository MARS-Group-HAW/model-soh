﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHBicycleModel.Parking;
using SOHBicycleModel.Rental;
using SOHBicycleModel.Steering;
using SOHCarModel.Parking;
using SOHCarModel.Rental;
using SOHCarModel.Steering;
using SOHDomain.Graph;
using SOHFerryModel.Station;
using SOHMultimodalModel.Routing;
using SOHTrainModel.Station;

namespace SOHMultimodalModel.Multimodal;

/// <summary>
///     This class provides an abstract implementation for the <see cref="IMultimodalLayer" />.
/// </summary>
public abstract class AbstractMultimodalLayer : AbstractLayer, IMultimodalLayer, IModalChoiceResolver
{
    private IMultimodalRouteFinder _routeFinder;

    /// <summary>
    ///     Provides the possibility to search multimodal routes, respecting
    ///     multiple different modalities given by layer.
    /// </summary>
    public IMultimodalRouteFinder RouteFinder =>
        _routeFinder ??= new MultimodalRouteFinder(SpatialGraphMediatorLayer);

    /// <summary>
    ///     Holds all environment layers for any movement.
    /// </summary>
    [PropertyDescription]
    public SpatialGraphMediatorLayer SpatialGraphMediatorLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="FerryStationLayer" /> holding all stations
    ///     where the <see cref="ModalChoice.Ferry" /> is available.
    /// </summary>
    [PropertyDescription]
    public FerryStationLayer FerryStationLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="TrainStationLayer" /> holding all stations
    ///     where the <see cref="ModalChoice.Train" /> is available.
    /// </summary>
    [PropertyDescription]
    public TrainStationLayer TrainStationLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="BicycleParkingLayer" /> holding all parking lots
    ///     where the <see cref="ModalChoice.CyclingOwnBike" /> can be parked.
    /// </summary>
    [PropertyDescription]
    public BicycleParkingLayer BicycleParkingLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="BicycleRentalLayer" /> holding all rental stations
    ///     where the <see cref="ModalChoice.CyclingRentalBike" /> is available.
    /// </summary>
    [PropertyDescription]
    public BicycleRentalLayer BicycleRentalLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="CarParkingLayer" /> holding all parking spaces
    ///     where the <see cref="ModalChoice.CarDriving" /> may start or end its drive.
    /// </summary>
    [PropertyDescription]
    public CarParkingLayer CarParkingLayer { get; set; }

    /// <summary>
    ///     Gets the <see cref="CarRentalLayer" /> holding all rental stations
    ///     where the <see cref="ModalChoice.CarRentalDriving" /> is available.
    /// </summary>
    [PropertyDescription]
    public CarRentalLayer CarRentalLayer { get; set; }

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
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        ModalChoice modalChoice)
    {
        return RouteFinder.Search(agent, start, goal, modalChoice);
    }

    public MultimodalRoute Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        IEnumerable<ModalChoice> capabilities)
    {
        return RouteFinder.Search(agent, start, goal, capabilities);
    }

    private IEnumerable<ModalChoice> ResolveModalChoice<T>(ISpatialNode source, ModalChoice modalChoice,
        IVectorLayer<T> vectorLayer) where T : IVectorFeature
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

    private bool ConsumeModalChoice<T>(ISpatialNode source, IVectorLayer<T> vectorLayer)
        where T : IVectorFeature
    {
        if (vectorLayer == null) return false;

        var feature = vectorLayer.Nearest(source.Position.PositionArray);
        if (feature == null) return false;

        var centroid = feature.VectorStructured.Geometry.Centroid;
        var featurePosition = Position.CreateGeoPosition(centroid.X, centroid.Y);
        var nearestNode = SpatialGraphMediatorLayer.Environment.NearestNode(featurePosition);

        if (!source.Equals(nearestNode)) return false;

        return feature is not IModalChoiceConsumer consumer || consumer.CanConsume();
    }
}