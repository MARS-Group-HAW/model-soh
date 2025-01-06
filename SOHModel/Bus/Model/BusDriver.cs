using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Bus.Route;
using SOHModel.Bus.Steering;
using SOHModel.Domain.Steering.Common;

namespace SOHModel.Bus.Model;

public class BusDriver : AbstractAgent, IBusSteeringCapable
{
    private static int _stableId;

    private long _startTickForCurrentStation;

    private BusDriver(UnregisterAgent unregister, BusLayer layer)
    {
        ID = Guid.NewGuid();
        _unregister = unregister;
        Layer = layer;
    }

    public BusDriver(BusLayer layer, UnregisterAgent unregister) : this(unregister, layer)
    {
        InitializeBus();
    }

    public BusDriver(BusLayer layer, UnregisterAgent unregister, string busType) : this(unregister, layer)
    {
        InitializeBus(busType);
    }

    public bool Boarding => AmountOfTicksAtCurrentStation <= MinimumBoardingTimeInSeconds || !DepartureTickArrived;
    public bool DepartureTickArrived => _departureTick <= Layer.Context.CurrentTick;

    private long AmountOfTicksAtCurrentStation => Layer.Context.CurrentTick - _startTickForCurrentStation;

    public BusRoute.BusRouteEnumerator BusRouteEnumerator =>
        (BusRoute.BusRouteEnumerator)(_busRouteEnumerator ??= BusRoute.GetEnumerator());

    public IEnumerable<BusRouteEntry> RemainingStations => BusRoute.Skip(BusRouteEnumerator.CurrentIndex);

    public BusRouteEntry CurrentBusRouteEntry => BusRouteEnumerator.Current;

    public int StationStops => BusRoute.Entries.IndexOf(BusRouteEnumerator.Current);

    [PropertyDescription(Name = "line")] public string Line { get; set; }

    [PropertyDescription(Name = "waitingInSeconds")]
    public int MinimumBoardingTimeInSeconds { get; set; }

    /// <summary>
    ///     The route will be proceeded in the opposite direction.
    /// </summary>
    [PropertyDescription(Name = "reversedRoute")]
    public bool ReversedRoute { get; set; }

    [PropertyDescription]
    public float LoadPercentage => (float)Math.Round(getLoadPercentage(), 2);

    [PropertyDescription]
    public int PassengerCount => Bus.Passengers?.Count ?? 0;

    [PropertyDescription]
    public int PassengerCapacity => Bus.PassengerCapacity;

    private float getLoadPercentage() {
        if (PassengerCount > 0)
        {
            return PassengerCount * 100 / (float)PassengerCapacity;
        }
        return 0;
    }

    [PropertyDescription] public BusLayer Layer { get; }
    private Mars.Interfaces.Environments.Route Route
    {
        get => _steeringHandle.Route;
        set => _steeringHandle.Route = value;
    }

    public bool GoalReached => _steeringHandle?.GoalReached ?? false;
    public Bus Bus { get; set; }
    public int StableId { get; } = _stableId++;

    public BusRoute BusRoute { get; set; }

    public Position Position
    {
        get => Bus.Position;
        set => Bus.Position = value;
    }

    public void Notify(PassengerMessage passengerMessage)
    {
        //do nothing, I am the driver
    }

    public bool OvertakingActivated => false;

    public bool BrakingActivated
    {
        get => false;
        set { }
    }

    private void InitializeBus(string type = "CapaCityL")
    {
        Bus = Layer.EntityManager.Create<Bus>("type", type);
        Bus.Layer = Layer;
        Bus.TryEnterDriver(this, out _steeringHandle);
    }

    public override void Tick()
    {
        if (BusRoute == null)
        {
            FindRouteAndStartCommuting();
            _departureTick = Layer.Context.CurrentTick;
        }

        if (!Boarding)
        {
            Bus.BusStation?.Leave(Bus);

            _steeringHandle.Move();

            if (GoalReached)
            {
                Environment.Remove(Bus);
                BusRouteEnumerator.Current?.To.Enter(Bus);

                var currentMinutes = BusRouteEnumerator.Current?.Minutes ?? 0;
                _departureTick += currentMinutes * 60;

                var notAtTerminalStation = FindNextRouteSection();
                if (notAtTerminalStation)
                {
                    Bus.NotifyPassengers(PassengerMessage.GoalReached);
                }
                else
                {
                    Bus.NotifyPassengers(PassengerMessage.TerminalStation);
                    _unregister(Layer, this);
                }
            }
        }
    }

    private void FindRouteAndStartCommuting()
    {
        if (Layer.BusRouteLayer.TryGetRoute(Line, out var schedule))
            BusRoute = schedule;
        else
            throw new ArgumentException($"No bus route provided by {nameof(BusRouteLayer)}");

        if (BusRoute.Count() < 1)
            throw new ArgumentException("Bus route requires at least two stops");

        if (ReversedRoute)
            BusRoute = BusRoute.Reversed();

        FindNextRouteSection();

        var busStation = BusRouteEnumerator.Current?.From;
        if (!busStation?.Enter(Bus) ?? true)
            throw new ArgumentException("Bus could not dock the first station");
    }

    private bool FindNextRouteSection()
    {
        if (BusRouteEnumerator.MoveNext())
        {
            var source = Environment.NearestNode(BusRouteEnumerator.Current?.From.Position,
                SpatialModalityType.CarDriving);
            var target = Environment.NearestNode(BusRouteEnumerator.Current?.To.Position,
                SpatialModalityType.CarDriving);

            var route = Environment.FindShortestRoute(source, target,
                edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));

            if (route is { Count: > 0 })
            {
                Route = route;
                _startTickForCurrentStation = Layer.Context.CurrentTick;
                Environment.Insert(Bus, Route.First().Edge.From);
                return true;
            }
        }

        return false;
    }

    #region fields

    private BusSteeringHandle _steeringHandle;
    private readonly UnregisterAgent _unregister;
    private long _departureTick;
    private ISpatialGraphEnvironment Environment => Layer.GraphEnvironment;

    private IEnumerator<BusRouteEntry> _busRouteEnumerator;

    #endregion
}