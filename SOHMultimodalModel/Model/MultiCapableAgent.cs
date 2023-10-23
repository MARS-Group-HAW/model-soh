using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Numerics.Statistics;
using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHBicycleModel.Rental;
using SOHCarModel.Model;
using SOHCarModel.Parking;
using SOHCarModel.Rental;
using SOHCarModel.Steering;
using SOHDomain.Graph;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Common;
using SOHFerryModel.Station;
using SOHFerryModel.Steering;
using SOHMultimodalModel.Commons;
using SOHMultimodalModel.Multimodal;
using SOHMultimodalModel.Routing;
using SOHTrainModel.Station;
using SOHTrainModel.Steering;

namespace SOHMultimodalModel.Model;

/// <summary>
///     This <see cref="MultiCapableAgent{TLayer}" /> implements different capabilities for instance: WALK, DRIVE and CYCLE
/// </summary>
public abstract class MultiCapableAgent<TLayer> : MultimodalAgent<TLayer>, IWalkingCapable, ICarSteeringCapable,
    IBicycleSteeringAndRentalCapable, ICarRentalCapable, IFerryPassenger, ITrainPassenger
    where TLayer : IMultimodalLayer
{
    private const double DeltaDistanceEqualsInM = 3d;
    private readonly bool[] _capabilities = new bool[Enum.GetNames(typeof(ModalChoice)).Length];
    private int _mainModalActualTravelTime;
    private bool _resultOutputStored;
    private Position _startPosition;
    private WalkingShoes _walkingShoes;

    public override void Init(TLayer layer)
    {
        base.Init(layer);

        Mass = 80;
        EnableCapability(ModalChoice.Walking);

        if (CapabilityDrivingOwnCar)
        {
            if (CarParkingLayer != null)
            {
                var radius = CarRadiusToStartPosition > 0 ? CarRadiusToStartPosition : 1000;
                Car ??= CarParkingLayer.CreateOwnCarNear(StartPosition, radius);
                Car ??= CarParkingLayer.CreateOwnCarNear(StartPosition);
            }
            else
            {
                throw new ApplicationException($"{nameof(CarParkingLayer)} is not initialized.");
            }
        }
    }

    public void SetWalking()
    {
        WalkingShoes.SetWalking();
    }

    public void SetRunning()
    {
        WalkingShoes.SetRunning();
    }

    #region input_params

    /// <summary>
    ///     Gets or set the gender type of this entity, affecting the walking and running speed
    /// </summary>
    [PropertyDescription(Name = "gender")]
    public GenderType Gender { get; set; }

    /// <summary>
    ///     Initial position at that the agent is placed.
    /// </summary>
    [PropertyDescription(Name = "startPosition")]
    public Position StartPosition
    {
        get => _startPosition;
        set
        {
            Position ??= value;
            _startPosition = value;
        }
    }

    /// <summary>
    ///     Gets or initializes the whole network environment where this agent is moving on.
    /// </summary>
    [PropertyDescription]
    public SpatialGraphMediatorLayer EnvironmentLayer { get; set; }

    #endregion

    #region output_params

    /// <summary>
    ///     Defines the radius from the start position within that the car is placed.
    ///     Takes the closest parking space if not set or smaller equal zero.
    /// </summary>
    public double CarRadiusToStartPosition { get; set; }

    public double PreferredSpeed
    {
        get => WalkingShoes.PreferredSpeed;
        protected set => WalkingShoes.PreferredSpeed = value;
    }

    public double PerceptionInMeter { get; set; }
    public double Bearing { get; set; }

    #endregion

    #region Capabilities

    /// <summary>
    ///     Sets the capability of this agent with given params.
    /// </summary>
    /// <param name="modalChoice">Identifies the capability.</param>
    /// <param name="active">Determines if the agent has the capability or not.</param>
    protected void EnableCapability(ModalChoice modalChoice, bool active = true)
    {
        _capabilities[(int)modalChoice] = active;
    }

    /// <summary>
    ///     Determines if given capability is active.
    /// </summary>
    /// <param name="modalChoice">Identifies the capability.</param>
    /// <returns>True, if capability is active, false otherwise</returns>
    protected bool IsCapabilityEnabled(ModalChoice modalChoice)
    {
        return _capabilities[(int)modalChoice];
    }

    internal bool TryEnterWalkingShoes(Position position)
    {
        if (!WalkingShoes.TryEnterDriver(this, out var steeringHandle)) return false;

        ActiveSteering = steeringHandle;

        var nearestNode = EnvironmentLayer.Environment.NearestNode(position);
        if (!EnvironmentLayer.Environment.Insert(WalkingShoes, nearestNode))
            throw new ApplicationException("Pedestrian could not be added to SpatialGraphEnvironment");

        steeringHandle.Position = nearestNode.Position;

        return true;
    }

    private bool TryLeaveSidewalk()
    {
        EnvironmentLayer.Environment.Remove(WalkingShoes);
        return true;
    }

    [PropertyDescription(Ignore = true)]
    public IEnumerable<ModalChoice> Capabilities =>
        _capabilities.Select((_, i) => (ModalChoice)i)
            .Where(c => _capabilities[(int)c]);

    [PropertyDescription(Ignore = true)] public string AgentCapabilities => string.Join("_", Capabilities);

    [PropertyDescription(Ignore = true)]
    public bool CapabilityDrivingOwnCar
    {
        get => IsCapabilityEnabled(ModalChoice.CarDriving);
        set => EnableCapability(ModalChoice.CarDriving, value);
    }

    [PropertyDescription(Ignore = true)]
    public bool CapabilityCycling
    {
        get => IsCapabilityEnabled(ModalChoice.CyclingRentalBike);
        set => EnableCapability(ModalChoice.CyclingRentalBike, value);
    }

    #endregion

    #region travel_time_output

    public int ExpectedTravelTime { get; protected set; }

    public bool StoreTickResult { get; set; }

    public int ActualTravelTime { get; private set; }

    public int RouteMainModalActualTravelTime
    {
        get => RouteMainModalChoice.Equals(ModalChoice.Walking) ? ActualTravelTime : _mainModalActualTravelTime;
        protected set => _mainModalActualTravelTime = value;
    }

    public string RouteMainModality => RouteMainModalChoice.ToString();


    public string RouteModalities =>
        MultimodalRoute?.Stops.Select(s => s.ModalChoice.ToString())
            .Aggregate((i, j) => i + "_" + j) ?? "";

    public int RouteModalityCount => MultimodalRoute?.Stops.Count ?? 0;

    private DateTime RouteMainModalActualTravelTimeStartTick { get; set; }

    public int RouteMainModalRouteLength
    {
        get
        {
            if (MultimodalRoute == null) return 0;
            if (!MultimodalRoute.Stops.Any()) return 0;
            if (MultimodalRoute.Stops.All(stop => stop.ModalChoice == ModalChoice.Walking))
                return (int)MultimodalRoute.RouteLength;

            var routeStop = MultimodalRoute.Stops.FirstOrDefault(stop => stop.ModalChoice != ModalChoice.Walking);
            if (routeStop != null) return (int)routeStop.Route.RouteLength;
            return 0;
        }
    }

    public override void Move()
    {
        base.Move();
        StoreRouteResultIfNecessary();
    }

    private void StoreRouteResultIfNecessary()
    {
        StoreTickResult = false;
        if (!GoalReached || _resultOutputStored || ExpectedTravelTime <= 0) return;

        ActualTravelTime = (int)Math.Round(SimulationTime.Subtract(CurrentMultimodalRouteStartTime).TotalSeconds);

        StoreTickResult = ActualTravelTime >= 0;
        _resultOutputStored = true;
    }

    protected override void ResetOutputProperties()
    {
        ExpectedTravelTime = MultimodalRoute.ExpectedTravelTime(this);
        RouteMainModalActualTravelTime = 0;
        _resultOutputStored = false;
    }

    #endregion

    #region switch modal type

    protected override bool EnterModalType(ModalChoice modalChoice, Route route)
    {
        if (!TryEnterModalType()) return false;

        if (modalChoice == RouteMainModalChoice)
            RouteMainModalActualTravelTimeStartTick = SimulationTime.AddSeconds(0);

        return true;

        bool TryEnterModalType()
        {
            switch (modalChoice)
            {
                case ModalChoice.Walking:
                    return IsValid(route) &&
                           TryEnterWalkingShoes(route.FirstOrDefault()?.Edge.From.Position) &&
                           SetRoute(route);
                case ModalChoice.CarDriving:
                    if (Car == null) return false;
                    return TryEnterVehicleAsDriver(Car, this) && TryLeaveCarParkingLayer() && SetRoute(route);
                case ModalChoice.CarRentalDriving:
                    //TODO reserve car when starting with route
                    //TODO rental cars on parking spots
                    var car = RentCar(Position);
                    if (car == null) return false;
                    return TryEnterVehicleAsDriver(car, this) && SetRoute(route);
                case ModalChoice.CyclingOwnBike:
                    if (Bicycle == null) return false;
                    return TryEnterVehicleAsDriver(Bicycle, this) && TryUnlockOwnBicycle() && SetRoute(route);
                case ModalChoice.CyclingRentalBike:
                    RentalBicycle = RentBicycle(Position);
                    if (RentalBicycle == null) return false;
                    return TryEnterVehicleAsDriver(RentalBicycle, this) && TryLeaveRentalStation() &&
                           SetRoute(route);
                case ModalChoice.Ferry:
                    var ferryStation = FerryStationLayer.Nearest(Position);
                    if (ferryStation == null) return false;
                    var ferry = ferryStation.Find(route.Goal);
                    return TryEnterVehicleAsPassenger(ferry, this);
                case ModalChoice.Train:
                    var trainStation = TrainStationLayer.Nearest(Position);
                    if (trainStation == null) return false;
                    var train = trainStation.Find(route.Goal);
                    return TryEnterVehicleAsPassenger(train, this);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            bool SetRoute(Route newRoute)
            {
                ActiveSteering.Route = newRoute;
                return true;
            }
        }
    }

    /// <summary>
    ///     Determines if the route is not null and not empty
    /// </summary>
    /// <param name="route">that is check for validity</param>
    /// <returns>true, if route is valid, false otherwise</returns>
    private static bool IsValid(Route route)
    {
        return route is { GoalReached: false } && route.FirstOrDefault() != null;
    }

    protected override bool LeaveModalType(ModalChoice modalChoice)
    {
        if (modalChoice == RouteMainModalChoice)
            RouteMainModalActualTravelTime =
                (int)Math.Round(SimulationTime.Subtract(RouteMainModalActualTravelTimeStartTick).TotalSeconds);
        return modalChoice switch
        {
            ModalChoice.Walking => TryLeaveVehicle(this) && TryLeaveSidewalk(),
            ModalChoice.CarDriving => TryEnterCarParkingLayer() && TryLeaveVehicle(this),
            ModalChoice.CarRentalDriving => TryLeaveVehicle(this), //TODO enter parking layer
            ModalChoice.CyclingRentalBike => TryEnterBicycleRentalStation() && TryLeaveVehicle(this),
            ModalChoice.CyclingOwnBike => TryParkOwnBicycle() && TryLeaveVehicle(this),
            ModalChoice.Ferry => TryLeaveVehicle(this),
            ModalChoice.Train => TryLeaveVehicle(this),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override void Notify(PassengerMessage passengerMessage)
    {
        switch (passengerMessage)
        {
            case PassengerMessage.TerminalStation:
            {
                if (LeaveModalType(MultimodalRoute.CurrentModalChoice))
                {
                    MultimodalRoute.CurrentRoute.JumpToGoal();
                    MultimodalRoute.Next();
                }

                break;
            }
            case PassengerMessage.GoalReached:
            {
                if (Position.DistanceInMTo(MultimodalRoute.CurrentRoute.Goal) < DeltaDistanceEqualsInM)
                    if (LeaveModalType(MultimodalRoute.CurrentModalChoice))
                    {
                        MultimodalRoute.CurrentRoute.JumpToGoal();
                        MultimodalRoute.Next();
                    }

                break;
            }
            case PassengerMessage.NoDriver:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(passengerMessage), passengerMessage, null);
        }
    }

    private bool TryEnterCarParkingLayer()
    {
        if (OnCarParkingLayer || Car.CarParkingLayer == null) return true;

        var carParkingSpace = Car.CarParkingLayer.Nearest(Position);
        if (carParkingSpace == null) return false;

        // var nearestNodeParkingSpace = EnvironmentLayer.StreetEnvironment.NearestNode(carParkingSpace.Position);
        // var nearestNodePos = EnvironmentLayer.StreetEnvironment.NearestNode(Position);
        // var arrived = nearestNodeParkingSpace
        //     .Equals(nearestNodePos);
        // if (!arrived) 
        //     return false;
        //TODO should be eliminated by improvements on the graph

        var enteredSuccessfully = carParkingSpace.Enter(Car);
        if (!enteredSuccessfully) return false;
        return EnvironmentLayer.Environment.Entities.ContainsKey(Car)
               && EnvironmentLayer.Environment.Remove(Car);
    }

    private bool TryLeaveCarParkingLayer()
    {
        if (!OnCarParkingLayer)
            return true;
        var carParkingSpace = Car.CarParkingSpace;

        var insertedInEvn = EnvironmentLayer.Environment.Entities.ContainsKey(Car) ||
                            EnvironmentLayer.Environment
                                .Insert(Car, EnvironmentLayer.Environment.NearestNode(carParkingSpace.Position));
        return insertedInEvn && carParkingSpace.Leave(Car);
    }

    /// <summary>
    ///     Enters the bicycle parking spot and leaves the bicycle there.
    /// </summary>
    private bool TryEnterBicycleRentalStation()
    {
        if (RentalBicycle == null) return true;

        var rentalStation = BicycleRentalLayer.Nearest(Position, false);
        if (rentalStation == null) throw new ApplicationException("Should always find a Bicycle rental station.");

        var success = rentalStation.Enter(RentalBicycle) &&
                      EnvironmentLayer.Environment.Remove(RentalBicycle);
        if (success) RentalBicycle = null;
        return success;
    }

    /// <summary>
    ///     Removes the own bicycle from the environment
    /// </summary>
    /// <returns>true on success, false otherwise</returns>
    private bool TryParkOwnBicycle()
    {
        return Bicycle != null && EnvironmentLayer.Environment.Remove(Bicycle);
    }

    /// <summary>
    ///     Leaves (if necessary) a parking lot and insert own bicycle into the environment.
    /// </summary>
    /// <returns>true on success, false otherwise</returns>
    private bool TryUnlockOwnBicycle()
    {
        if (Bicycle == null) return false;

        Bicycle.BicycleParkingLot = null;
        return EnvironmentLayer.Environment.Entities.ContainsKey(Bicycle) ||
               EnvironmentLayer.Environment
                   .Insert(Bicycle, EnvironmentLayer.Environment.NearestNode(MultimodalRoute.Start));
    }

    /// <summary>
    ///     Leaves the bicycle parking spot and enters the environment for further movement.
    /// </summary>
    /// <returns>true on success, false otherwise</returns>
    private bool TryLeaveRentalStation()
    {
        if (RentalBicycle.BicycleRentalStation == null) return true;
        var rentalStation = RentalBicycle.BicycleRentalStation;


        var env = EnvironmentLayer.Environment ??
                  throw new InvalidOperationException(
                      "No street or cycle path environment is provided for bicycle rental action");

        var insertedInEvn = env.Entities.ContainsKey(RentalBicycle) ||
                            env.Insert(RentalBicycle,
                                env.NearestNode(rentalStation.Position));
        return insertedInEvn && rentalStation.Leave(RentalBicycle);
    }

    protected RentalBicycle RentBicycle(Position position)
    {
        var station = BicycleRentalLayer.Nearest(position, true);

        var bicycle = station?.RentAny();
        if (bicycle == null) return null;

        bicycle.BicycleRentalLayer = BicycleRentalLayer;
        return bicycle as RentalBicycle;
    }


    private RentalCar RentCar(Position position)
    {
        var rentalCar = CarRentalLayer.Nearest(position);
        return CarRentalLayer.Remove(rentalCar) ? rentalCar : null;
    }

    #endregion

    #region capability properties

    /// <summary>
    ///     Holds a personal <see cref="Bicycle" /> if the agent possesses one.
    /// </summary>
    public Bicycle Bicycle { get; protected set; }

    /// <summary>
    ///     Holds a <see cref="RentalBicycle" /> if one is leased.
    /// </summary>
    public RentalBicycle RentalBicycle { get; private set; }


    public Car Car { get; set; }

    [PropertyDescription] public IFerryStationLayer FerryStationLayer { get; set; }

    [PropertyDescription] public ITrainStationLayer TrainStationLayer { get; set; }

    [PropertyDescription] public IBicycleRentalLayer BicycleRentalLayer { get; set; }

    [PropertyDescription] public ICarRentalLayer CarRentalLayer { get; set; }


    [PropertyDescription] public ICarParkingLayer CarParkingLayer { get; set; }

    /// <summary>
    ///     The currently active modal type.
    /// </summary>
    [PropertyDescription]
    public ModalChoice ActiveCapability => MultimodalRoute?.CurrentModalChoice ?? ModalChoice.Walking;

    private bool OnCarParkingLayer => Car?.CarParkingSpace != null;


    [PropertyDescription(Ignore = true)] public bool OvertakingActivated { get; set; }
    public bool BrakingActivated { get; set; }

    [PropertyDescription] public bool CurrentlyCarDriving => Car?.Driver?.Equals(this) ?? false;

    [PropertyDescription(Ignore = true)] public double DriverRandom => HandleDriverType.DetermineDriverRand(DriverType);

    [PropertyDescription(Ignore = true)] public DriverType DriverType => DriverType.Normal;

    [PropertyDescription(Ignore = true)]
    public double CyclingPower { get; } =
        new FastGaussianDistributionD(75, 3).Next(RandomHelper.Random);

    [PropertyDescription] public double Mass { get; set; }

    [PropertyDescription(Ignore = true)] public double Gradient { get; } = 0;

    /// <summary>
    ///     The shoes are the physical representation of the multimodal agent within the environment.
    /// </summary>
    public WalkingShoes WalkingShoes
    {
        get
        {
            if (_walkingShoes != null) return _walkingShoes;
            var walkingSpeed = PedestrianAverageSpeedGenerator.CalculateWalkingSpeed(Gender);
            var runningSpeed = PedestrianAverageSpeedGenerator.CalculateRunningSpeed(Gender);
            return _walkingShoes = new WalkingShoes(EnvironmentLayer, walkingSpeed, runningSpeed);
        }
        private set => _walkingShoes = value;
    }

    #endregion
}