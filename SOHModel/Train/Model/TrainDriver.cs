using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.Train.Route;
using SOHModel.Train.Steering;

namespace SOHModel.Train.Model;

public class TrainDriver : AbstractAgent, ITrainSteeringCapable
{
    private static int _stableId;

    private long _startTickForCurrentStation;

    private TrainDriver(UnregisterAgent unregister, TrainLayer layer)
    {
        ID = Guid.NewGuid();
        _unregister = unregister;
        Layer = layer;
    }

    public TrainDriver(TrainLayer layer, UnregisterAgent unregister) : this(unregister, layer)
    {
        InitializeTrain();
    }

    public TrainDriver(TrainLayer layer, UnregisterAgent unregister, string trainType) : this(unregister, layer)
    {
        InitializeTrain(trainType);
    }

    public bool Boarding => AmountOfTicksAtCurrentStation <= MinimumBoardingTimeInSeconds || !DepartureTickArrived;
    public bool DepartureTickArrived => _departureTick <= Layer.Context.CurrentTick;

    private long AmountOfTicksAtCurrentStation => Layer.Context.CurrentTick - _startTickForCurrentStation;

    public TrainRoute.TrainRouteEnumerator TrainRouteEnumerator =>
        (TrainRoute.TrainRouteEnumerator)(_trainRouteEnumerator ??= TrainRoute.GetEnumerator());

    public IEnumerable<TrainRouteEntry> RemainingStations => TrainRoute.Skip(TrainRouteEnumerator.CurrentIndex);

    public TrainRouteEntry CurrentTrainRouteEntry => TrainRouteEnumerator.Current;

    public int StationStops => TrainRoute.Entries.IndexOf(TrainRouteEnumerator.Current);

    [PropertyDescription(Name = "line")] public string Line { get; set; }

    [PropertyDescription(Name = "waitingInSeconds")]
    public int MinimumBoardingTimeInSeconds { get; set; }

    /// <summary>
    ///     The route will be proceeded in the opposite direction.
    /// </summary>
    [PropertyDescription(Name = "reversedRoute")]
    public bool ReversedRoute { get; set; }

    [PropertyDescription] public TrainLayer Layer { get; }

    private Mars.Interfaces.Environments.Route Route
    {
        get => _steeringHandle.Route;
        set => _steeringHandle.Route = value;
    }

    public bool GoalReached => _steeringHandle?.GoalReached ?? false;
    public Train Train { get; set; }
    public int StableId { get; } = _stableId++;

    public TrainRoute TrainRoute { get; set; }

    public Position Position
    {
        get => Train.Position;
        set => Train.Position = value;
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

    private void InitializeTrain(string type = "HHA-Typ-DT5")
    {
        Train = Layer.EntityManager.Create<Train>("type", type);
        Train.Layer = Layer;
        Train.TryEnterDriver(this, out _steeringHandle);
    }

    public override void Tick()
    {
        if (TrainRoute == null)
        {
            FindTrainRouteAndStartCommuting();
            _departureTick = Layer.Context.CurrentTick;
        }

        if (!Boarding)
        {
            Train.TrainStation?.Leave(Train);

            _steeringHandle.Move();

            if (GoalReached)
            {
                Environment.Remove(Train);
                TrainRouteEnumerator.Current?.To.Enter(Train);

                var currentMinutes = TrainRouteEnumerator.Current?.Minutes ?? 0;
                _departureTick += currentMinutes * 60;

                var notAtTerminalStation = FindNextRouteSection();
                if (notAtTerminalStation)
                {
                    Train.NotifyPassengers(PassengerMessage.GoalReached);
                }
                else
                {
                    Train.NotifyPassengers(PassengerMessage.TerminalStation);
                    _unregister(Layer, this);
                }
            }
        }
    }

    private void FindTrainRouteAndStartCommuting()
    {
        if (Layer.TrainRouteLayer.TryGetRoute(Line, out var schedule))
            TrainRoute = schedule;
        else
            throw new ArgumentException($"No train route provided by {nameof(TrainRouteLayer)}");

        if (TrainRoute.Count() < 2)
            throw new ArgumentException("Train route requires at least two stops");

        if (ReversedRoute)
            TrainRoute = TrainRoute.Reversed();

        FindNextRouteSection();

        var trainStation = TrainRouteEnumerator.Current?.From;
        if (!trainStation?.Enter(Train) ?? true)
            throw new ArgumentException("Train could not dock the first station");
    }

    private bool FindNextRouteSection()
    {
        if (!TrainRouteEnumerator.MoveNext()) return false;

        var source = Environment.NearestNode(TrainRouteEnumerator.Current?.From.Position,
            SpatialModalityType.TrainDriving);
        var target =
            Environment.NearestNode(TrainRouteEnumerator.Current?.To.Position, SpatialModalityType.TrainDriving);

        Route = Environment.FindShortestRoute(source, target,
            edge => edge.Modalities.Contains(SpatialModalityType.TrainDriving));

        if (Route == null || Route.Count == 0)
            throw new ApplicationException(
                $"{nameof(TrainDriver)} cannot find route from '{source.Position}' " +
                $"to '{target.Position}' but: {Environment.Nodes.Count}");

        _startTickForCurrentStation = Layer.Context.CurrentTick;
        Environment.Insert(Train, Route.First().Edge.From);
        return true;
    }


    #region fields

    private TrainSteeringHandle _steeringHandle;
    private readonly UnregisterAgent _unregister;
    private long _departureTick;
    private ISpatialGraphEnvironment Environment => Layer.GraphEnvironment;

    private IEnumerator<TrainRouteEntry> _trainRouteEnumerator;

    #endregion
}