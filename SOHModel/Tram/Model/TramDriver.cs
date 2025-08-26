using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.Tram.Route;
using SOHModel.Tram.Steering;

namespace SOHModel.Tram.Model
{
    public class TramDriver : AbstractAgent, ITramSteeringCapable
    {
        private static int _stableId;
        private long _startTickForCurrentStation;

        private TramDriver(UnregisterAgent unregister, TramLayer layer)
        {
            ID = Guid.NewGuid();
            _unregister = unregister;
            Layer = layer;
        }

        public TramDriver(TramLayer layer, UnregisterAgent unregister) : this(unregister, layer)
        {
            InitializeTram();
        }

        public TramDriver(TramLayer layer, UnregisterAgent unregister, string tramType) : this(unregister, layer)
        {
            InitializeTram(tramType);
        }

        public bool Boarding => AmountOfTicksAtCurrentStation <= MinimumBoardingTimeInSeconds || !DepartureTickArrived;
        public bool DepartureTickArrived => _departureTick <= Layer.Context.CurrentTick;

        private long AmountOfTicksAtCurrentStation => Layer.Context.CurrentTick - _startTickForCurrentStation;

        public TramRoute.TramRouteEnumerator TramRouteEnumerator =>
            (TramRoute.TramRouteEnumerator)(_tramRouteEnumerator ??= TramRoute.GetEnumerator());

        public IEnumerable<TramRouteEntry> RemainingStations => TramRoute.Skip(TramRouteEnumerator.CurrentIndex);

        public TramRouteEntry CurrentTramRouteEntry => TramRouteEnumerator.Current;

        public int StationStops => TramRoute.Entries.IndexOf(TramRouteEnumerator.Current);

        [PropertyDescription(Name = "line")] public string Line { get; set; }

        [PropertyDescription(Name = "waitingInSeconds")]
        public int MinimumBoardingTimeInSeconds { get; set; }

        /// <summary>The route will be proceeded in the opposite direction.</summary>
        [PropertyDescription(Name = "reversedRoute")]
        public bool ReversedRoute { get; set; }

        [PropertyDescription] public TramLayer Layer { get; }

        private Mars.Interfaces.Environments.Route Route
        {
            get => _steeringHandle.Route;
            set => _steeringHandle.Route = value;
        }

        public bool GoalReached => _steeringHandle?.GoalReached ?? false;
        public Tram Tram { get; set; }
        public int StableId { get; } = _stableId++;

        public TramRoute TramRoute { get; set; }

        public Position Position
        {
            get => Tram.Position;
            set => Tram.Position = value;
        }

        public void Notify(PassengerMessage passengerMessage)
        {
            // do nothing; I am the driver
        }

        public bool OvertakingActivated => false;

        public bool BrakingActivated
        {
            get => false;
            set { }
        }

        private void InitializeTram(string type = "Alstom Citadis")
        {
            Tram = Layer.EntityManager.Create<Tram>("type", type);
            Tram.Layer = Layer;

            if (!Tram.TryEnterDriver(this, out _steeringHandle) || _steeringHandle == null)
                throw new InvalidOperationException("Failed to enter tram driver; steering handle not created.");
        }

        public override void Tick()
        {
            // IMPORTANT: mirror TrainDriver — initialize when route is missing
            if (TramRoute == null)
            {
                FindTramRouteAndStartCommuting();
                _departureTick = Layer.Context.CurrentTick;
            }

            if (!Boarding)
            {
                Tram.TramStation?.Leave(Tram);

                _steeringHandle.Move();

                if (GoalReached)
                {
                    Environment.Remove(Tram);
                    TramRouteEnumerator.Current?.To.Enter(Tram);

                    var currentMinutes = TramRouteEnumerator.Current?.Minutes ?? 0;
                    _departureTick += currentMinutes * 60;

                    var notAtTerminalStation = FindNextRouteSection();
                    if (notAtTerminalStation)
                    {
                        Tram.NotifyPassengers(PassengerMessage.GoalReached);
                    }
                    else
                    {
                        Tram.NotifyPassengers(PassengerMessage.TerminalStation);
                        _unregister(Layer, this);
                    }
                }
            }
        }

        private void FindTramRouteAndStartCommuting()
        {
            if (Layer.TramRouteLayer.TryGetRoute(Line, out var schedule))
                TramRoute = schedule;
            else
                throw new ArgumentException($"No tram route provided by {nameof(TramRouteLayer)}");

            if (TramRoute.Count() < 2)
                throw new ArgumentException("Tram route requires at least two stops");

            if (ReversedRoute)
                TramRoute = TramRoute.Reversed();

            FindNextRouteSection();

            var tramStation = TramRouteEnumerator.Current?.From;
            if (!tramStation?.Enter(Tram) ?? true)
                throw new ArgumentException("Tram could not dock the first station");
        }

        private bool FindNextRouteSection()
        {
            if (!TramRouteEnumerator.MoveNext()) return false;

            var source = Environment.NearestNode(TramRouteEnumerator.Current?.From.Position,
                SpatialModalityType.TrainDriving);
            var target = Environment.NearestNode(TramRouteEnumerator.Current?.To.Position,
                SpatialModalityType.TrainDriving);

            Route = Environment.FindShortestRoute(source, target,
                edge => edge.Modalities.Contains(SpatialModalityType.TrainDriving));

            if (Route == null || Route.Count == 0)
                throw new ApplicationException(
                    $"{nameof(TramDriver)} cannot find route from '{source.Position}' " +
                    $"to '{target.Position}' but: {Environment.Nodes.Count}");

            _startTickForCurrentStation = Layer.Context.CurrentTick;
            Environment.Insert(Tram, Route.First().Edge.From);
            return true;
        }

        #region fields

        private TramSteeringHandle _steeringHandle;
        private readonly UnregisterAgent _unregister;
        private long _departureTick;
        private ISpatialGraphEnvironment Environment => Layer.GraphEnvironment;
        private IEnumerator<TramRouteEntry> _tramRouteEnumerator;

        #endregion
    }
}