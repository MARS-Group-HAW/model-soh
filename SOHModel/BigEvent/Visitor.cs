using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Bicycle.Parking;
using SOHModel.Bus.Station;
using SOHModel.Multimodal.Model;


namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{BaseWalkingLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<BaseWalkingLayer>
{
    private ISet<ModalChoice> _choices;
    private ModalChoice _preferred;
    private readonly Random _random = new Random();

    private static readonly Dictionary<string, string[]> _stationsByLine = new()
    {
        { "180", new[] { "de:02000:84032::840015", "de:02000:84030::840093" } },
        { "380(Shuttle)", new[] { "de:02000:84008::1101","de:02000:21000::2100" } },
        { "22", new[] { "de:02000:84050::840181", "de:02000:84030::840093"} }
    };

    [PropertyDescription] public IBicycleParkingLayer BicycleParkingLayer { get; set; }

    /**
     * This method initializes the visitor and sets the modal choices.
     */
    public override void Init(BaseWalkingLayer layer)
    {
        base.Init(layer);
        OvertakingActivated = true;
        _choices = new ModalityChooser().Evaluate(this);
        _choices.Add(ModalChoice.Walking);

        HandleLogic();
        const int radiusInM = 1000;

        if (_choices.Contains(ModalChoice.CyclingOwnBike) && BicycleParkingLayer != null)
        {
            Bicycle = BicycleParkingLayer.CreateOwnBicycleNear(StartPosition, radiusInM, 1.0);
            //Console.WriteLine("Bike created at " + Bicycle.Position);
        }

        if (_choices.Contains(ModalChoice.CarDriving) && CarParkingLayer != null)
        {
            Car = PickRandomCarFromParkingLayer();
        }
    }

    /**
     * This method picks a random car from the Barclays parking spots and removes it from the parking spot. 
     */
    private Car.Model.Car PickRandomCarFromParkingLayer()
    {
        if (BarclaysParkingLayer.ParkedCars.Count > 0)
        {
            var randomIndex = _random.Next(BarclaysParkingLayer.ParkedCars.Count);
            var randomCar = BarclaysParkingLayer.ParkedCars[randomIndex];
            BarclaysParkingLayer.ParkedCars.RemoveAt(randomIndex);
            return randomCar;
        }
        throw new ArgumentException("No cars available in the Barclays parking layer. Please ensure that there are " +
                                    "maximum 3660 car drivers in the simulation.");
    }


    /**
     * This method handles the logic of the modal choices.
     * It removes the modal choices that are not compatible with the selected modal choice.
     * For example, if the visitor chooses to drive a car, they won't be able to choose to take the bus or train or co-drive the car.
     */
    private void HandleLogic()
    {
        var modalProbabilities = new Dictionary<ModalChoice, double>
        {
            { ModalChoice.CarDriving, UsesCar },
            { ModalChoice.Bus, UsesBus },
            { ModalChoice.Train, UsesTrain },
            { ModalChoice.CyclingOwnBike, UsesBike }
        };

        var filteredChoices = _choices
            .Where(choice => modalProbabilities.ContainsKey(choice))
            .ToDictionary(choice => choice, choice => modalProbabilities[choice]);

        if (filteredChoices.Any())
        {
            _preferred = filteredChoices
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;
        }
        else
        {
            _preferred = ModalChoice.Walking;
        }
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        return _choices;
    }



    protected override MultimodalRoute FindMultimodalRoute()
    {
        if (_preferred.Equals(ModalChoice.Bus)) // If the preferred modality is the bus, randomly choose a bus line with corresponding start and end station and find a multimodal route.
        {
            BusStation[] stations = GetRandomStartAndEndStation();
            return FindMultimodalRoute(stations[0], stations[1]);
        }
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, _preferred);
    }


    /**
     * This method finds a multimodal route between the start and the goal destination of a visitor using the bus.
     * As the default bus route algorithm only finds corresponding bus stations by distance (as of 01/2025), this method takes to bus stations with intersecting lines as input.
     * It then builds a multimodal route from the start to the end destination by first walking to the start station, then taking the bus to the end station and finally walking to the goal destination.
    */
    private MultimodalRoute FindMultimodalRoute(BusStation startStation, BusStation endStation)
    {
        if (!startStation.Lines.Intersect(endStation.Lines).Any()) throw new ArgumentException("The start and end station do not share a common line.");
        MultimodalRoute routeToStartStation = MultimodalLayer.Search(this, StartPosition, startStation.Position, ModalChoice.Walking);
        MultimodalRoute busRouteBetweenStations = MultimodalLayer.Search(this, startStation.Position, endStation.Position, ModalChoice.Bus);
        MultimodalRoute routeFromEndStation = MultimodalLayer.Search(this, endStation.Position, GoalPosition, ModalChoice.Walking);

        AppendMultiModalRoute(routeToStartStation, busRouteBetweenStations);
        AppendMultiModalRoute(routeToStartStation, routeFromEndStation);

        return routeToStartStation;
    }

    private void AppendMultiModalRoute(MultimodalRoute route1, MultimodalRoute route2)
    {
        foreach (RouteStop routeStop in route2.Stops)
        {
            route1.Add(routeStop.Route, routeStop.ModalChoice);
        }
    }

    private BusStation[] GetRandomStartAndEndStation()
    {
        if (BusStationLayer == null)
        {
            throw new ArgumentException("BusStationLayer is null. Please ensure that the BusStationLayer is set.");
        }
        var randomIndex = _random.Next(_stationsByLine.Count);
        var line = _stationsByLine.Keys.ElementAt(randomIndex);
        if (BusStationLayer is BusStationLayer busStationLayer)
        {
            return [.. _stationsByLine[line].Select(busStationLayer.Find)];
        }
        throw new ArgumentException("BusStationLayer is not of type BusStationLayer. Please ensure that the BusStationLayer is set correctly.");
    }


    #region input

    [PropertyDescription(Name = "usesBike")]
    public double UsesBike { get; set; }

    [PropertyDescription(Name = "usesCar")]
    public double UsesCar { get; set; }

    [PropertyDescription(Name = "usesTrain")]
    public double UsesTrain { get; set; }

    [PropertyDescription(Name = "usesBus")]
    public double UsesBus { get; set; }

    #endregion
}

public class ModalityChooser
{
    public ISet<ModalChoice> Evaluate(Visitor attributes)
    {
        HashSet<ModalChoice> choices = new();
        if (RandomHelper.Random.NextDouble() < attributes.UsesCar)
            choices.Add(ModalChoice.CarDriving);

        if (RandomHelper.Random.NextDouble() < attributes.UsesBike)
            choices.Add(ModalChoice.CyclingOwnBike);

        if (RandomHelper.Random.NextDouble() < attributes.UsesTrain)
            choices.Add(ModalChoice.Train);

        if (RandomHelper.Random.NextDouble() < attributes.UsesBus)
            choices.Add(ModalChoice.Bus);

        return choices;
    }
}