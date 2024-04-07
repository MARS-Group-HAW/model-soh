using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Common;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Multimodal.Multimodal;

/// <summary>
///     The multimodal agent can handle any modal type and thus any change between modal types. It has a representational
///     road user that holds physical sizes and is placed in the environment.
/// </summary>
public abstract class MultimodalAgent<TLayer> : IAgent<TLayer>, IModalCapabilitiesAgent, IPassengerCapable
    where TLayer : IMultimodalLayer
{
    private Position _position;
    private double _velocity;

    [PropertyDescription(Ignore = true)]
    public double Velocity
    {
        get => ActiveSteering?.Velocity ?? _velocity;
        set => _velocity = value;
    }

    private bool EnterRequired => ActiveSteering == null;

    private bool SwitchRequired => ActiveSteering?.GoalReached ?? false;

    protected bool Offside => Whereabouts == Whereabouts.Offside;

    private bool InVehicle => Whereabouts == Whereabouts.Vehicle;

    /// <summary>
    ///     Gets the multimodal layer which manage this entity.
    /// </summary>
    [PropertyDescription]
    public IMultimodalLayer MultimodalLayer { get; set; }

    public virtual void Init(TLayer layer)
    {
        if (layer == null)
            throw new ArgumentException("MultimodalAgent requires an IMultimodalLayer as init input.");

        MultimodalLayer = layer;
    }

    public abstract void Tick();

    public Guid ID { get; set; } = Guid.NewGuid();

    public Position Position
    {
        get => ActiveSteering?.Position ?? _position;
        set => _position = value;
    }

    public virtual void Move()
    {
        if (MultimodalRoute == null || MultimodalRoute.GoalReached) return;
        if (EnterRequired) //start with current route
            if (!EnterModalType(MultimodalRoute.CurrentModalChoice, MultimodalRoute.CurrentRoute) &&
                MultimodalRoute.CurrentRoute.GoalReached)
                MultimodalRoute.Next();

        if (SwitchRequired) // leave old modality, continue with next one
        {
            var previousModalType = MultimodalRoute.CurrentModalChoice;
            MultimodalRoute.Next();

            if (LeaveModalType(previousModalType))
            {
                if (!EnterModalType(MultimodalRoute.CurrentModalChoice, MultimodalRoute.CurrentRoute)) return;
            }
            else if (!MultimodalRoute.GoalReached)
            {
                ReRouteToGoal();
            }
        }

        ActiveSteering?.Move();

        if (MultimodalRoute.GoalReached)
        {
            if (InVehicle &&
                !LeaveModalType(MultimodalRoute
                    .CurrentModalChoice)) // goal reached but vehicle cannot be parked here
                ReRouteToGoal();
            if (OnSidewalk) LeaveModalType(MultimodalRoute.CurrentModalChoice);
        }
    }

    /// <summary>
    ///     Reroutes with new switching points and appends to multimodal route. Then sets active steering route.
    /// </summary>
    protected void ReRouteToGoal()
    {
        if (MultimodalRoute.Goal == null)
            throw new ApplicationException("Reroute failed because goal is missing!?");
        if (ActiveSteering == null)
            throw new ApplicationException("Reroute failed because no active steering!?");

        var multimodalRoute = MultimodalLayer.Search(this, Position, MultimodalRoute.Goal,
            MultimodalRoute.MainModalChoice);
        MultimodalRoute.AppendAndDeleteTail(multimodalRoute);
        ActiveSteering.Route = MultimodalRoute.CurrentRoute;
    }

    /// <summary>
    ///     The agent has to enter into given modal type.
    /// </summary>
    /// <param name="modalChoice">that should be the next active modal type.</param>
    /// <param name="route">that should be the next active route.</param>
    /// <returns>Success of switching.</returns>
    protected abstract bool EnterModalType(ModalChoice modalChoice, Route route);

    /// <summary>
    ///     The agent has to leave given modal type.
    /// </summary>
    /// <param name="modalChoice">that will be left.</param>
    /// <returns>Success of leaving.</returns>
    protected abstract bool LeaveModalType(ModalChoice modalChoice);


    /// <summary>
    ///     Tries to enter a vehicle as the driver. If this succeeds, the pedestrian leaves the sidewalk (switch of
    ///     modal context) and sets a new route.
    /// </summary>
    /// <param name="vehicle">That should be entered.</param>
    /// <param name="driver">Who wants to enter the vehicle.</param>
    /// <returns>Whether the operation was successful or not.</returns>
    public bool TryEnterVehicleAsDriver<TSteeringCapable, TSteeringHandle, TPassengerHandle>(
        Vehicle<TSteeringCapable, IPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        TSteeringCapable driver)
        where TSteeringCapable : ISteeringCapable
        where TSteeringHandle : ISteeringHandle
        where TPassengerHandle : IPassengerHandle
    {
        if (!vehicle.TryEnterDriver(driver, out var steeringHandle)) return false;

        ActiveSteering = steeringHandle;
        return true;
    }

    /// <summary>
    ///     Tries to enter a vehicle as a passengerCapable. If this succeeds, the pedestrian leaves the sidewalk
    ///     (switch of modal context).
    /// </summary>
    /// <param name="vehicle">That should be entered.</param>
    /// <param name="passenger">Who wants to enter the vehicle.</param>
    /// <returns>Whether the operation was successful or not.</returns>
    public bool TryEnterVehicleAsPassenger<TSteeringCapable, TSteeringHandle, TPassengerHandle>(
        Vehicle<TSteeringCapable, IPassengerCapable, TSteeringHandle, TPassengerHandle> vehicle,
        ISteeringCapable passenger)
        where TSteeringCapable : ISteeringCapable
        where TSteeringHandle : ISteeringHandle
        where TPassengerHandle : IPassengerHandle
    {
        if (vehicle == null || !vehicle.TryEnterPassenger(passenger, out var passengerHandle)) return false;

        // LeaveSidewalk();
        ActiveSteering = new IdlePassengerSteeringHandle(passengerHandle);
        return true;
    }

    /// <summary>
    ///     Leaves the current vehicle and enters the sidewalk.
    /// </summary>
    /// <param name="passengerCapable">Who wants to leave the vehicle.</param>
    /// <returns>Whether the operation was successful or not.</returns>
    public bool TryLeaveVehicle(IPassengerCapable passengerCapable)
    {
        if (ActiveSteering == null) return false;
        if (!ActiveSteering.LeaveVehicle(passengerCapable))
            return false;

        Position = ActiveSteering.Position;
        ActiveSteering = null;
        return true;
    }

    #region multimodal properties

    public ISet<ModalChoice> ModalChoices { get; protected set; }

    public ISimulationContext Context => MultimodalLayer?.Context;

    protected DateTime SimulationTime => Context.CurrentTimePoint ?? new DateTime(1999, 12, 31);

    [PropertyDescription(Name = "discriminator")]
    public int StableId { get; set; }

    /// <summary>
    ///     The full length ot the multimodal route.
    /// </summary>
    public int RouteLength => (int)(MultimodalRoute?.RouteLength ?? 0);

    [PropertyDescription(Ignore = true)]
    public ModalChoice RouteMainModalChoice => MultimodalRoute?.MainModalChoice ?? ModalChoice.Walking;

    public abstract void Notify(PassengerMessage passengerMessage);

    /// <summary>
    ///     Determines whether the agent is inserted in the primary steering environment or not
    /// </summary>
    protected bool OnSidewalk => Whereabouts == Whereabouts.Sidewalk;

    /// <summary>
    ///     Determines if the agent is on the sidewalk (walking), in a vehicle (driving or co-driving) or outside of all
    ///     environments.
    /// </summary>
    [PropertyDescription(Ignore = true)]
    public Whereabouts Whereabouts
    {
        get
        {
            return ActiveSteering switch
            {
                null => Whereabouts.Offside,
                WalkingSteeringHandle => Whereabouts.Sidewalk,
                _ => Whereabouts.Vehicle
            };
        }
    }

    public MultimodalRoute MultimodalRoute
    {
        get => _multimodalRoute;
        set
        {
            if (InVehicle) LeaveModalType(_multimodalRoute.CurrentModalChoice);

            if (OnSidewalk) LeaveModalType(ModalChoice.Walking);

            _multimodalRoute = value;
            CurrentMultimodalRouteStartTime = SimulationTime.AddSeconds(0);
            ResetOutputProperties();
        }
    }

    /// <summary>
    ///     Hook method for output properties.
    /// </summary>
    protected virtual void ResetOutputProperties()
    {
        //override by subtypes
    }

    /// <summary>
    ///     Contains the <see cref="DateTime" /> of the moment a new <see cref="MultimodalRoute" /> is assigned
    /// </summary>
    protected DateTime CurrentMultimodalRouteStartTime { get; private set; }

    [PropertyDescription(Ignore = true)] public bool GoalReached => MultimodalRoute?.GoalReached ?? false;

    /// <summary>
    ///     The currently used and thus active steering handle. Is never null.
    /// </summary>
    protected ISteeringHandle ActiveSteering { get; set; }

    private MultimodalRoute _multimodalRoute;

    /// <summary>
    ///     Provides the steering handle that is used whenever another active steering is invalidated.
    ///     In urban cases that could be a walking steering.
    /// </summary>
    // protected abstract ISteeringHandle PrimarySteeringHandle { get; } //TODO delete

    #endregion

    #region output_porperties

    public double DistanceStartGoal
    {
        get
        {
            if (MultimodalRoute?.Goal == null) return 0;
            return MultimodalRoute?.Start?.DistanceInMTo(MultimodalRoute?.Goal) ?? 0;
        }
    }

    // public string StartLatLong => StartLatitude + " " + StartLongitude;
    public double StartY => MultimodalRoute?.Start?.Latitude ?? 0;

    public double StartX => MultimodalRoute?.Start?.Longitude ?? 0;

    // public string GoalLatLong => GoalLatitude + " " + GoalLongitude;

    public double GoalY => MultimodalRoute?.Goal?.Latitude ?? 0;


    public double GoalX => MultimodalRoute?.Goal?.Longitude ?? 0;

    #endregion
}