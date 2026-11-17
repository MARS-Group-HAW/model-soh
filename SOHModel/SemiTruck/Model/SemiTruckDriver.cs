using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.Model.Driver.Services;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.RealTimeData;
using SOHModel.SemiTruck.Steering;
using Position = Mars.Interfaces.Environments.Position;
using SOHModel.Database;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents an autonomous agent that simulates a semi-truck driver in a spatial simulation.
    /// Refactored to use composition and separation of concerns for improved maintainability.
    /// </summary>
    public class SemiTruckDriver : IAgent<SemiTruckLayer>, ISemiTruckSteeringCapable
    {
        // Core dependencies
        private SemiTruckSteeringHandle _steeringHandle;
        private UnregisterAgent _unregister;
        private ISpatialGraphEnvironment _environment;
        private SemiTruckLayer _layer;

        // State managers
        private readonly RestState _restState;
        private readonly RefuelState _refuelState;
        private readonly AccidentState _accidentState;
        private FuelConsumptionTracker _fuelTracker; // created in Init, needs the truck
        private readonly SpeedAdjuster _speedAdjuster;
        private RouteManager _routeManager; // Initialized after truck creation

        // Properties required for initialization
        [PropertyDescription] public SemiTruckWeatherLayer WeatherLayer { get; set; }
        public double DefaultAccidentsPerYear { get; set; }
        public SemiTruck SemiTruck { get; set; }

        /// <summary>Driving time and break bookkeeping according to EU 561/2006.</summary>
        public DrivingTimeTracker DrivingTime { get; } = new();
        internal FuelConsumptionTracker FuelTracker => _fuelTracker;
        public TruckTripOutcome? TripOutcome { get; private set; }

        // Constructor
        public SemiTruckDriver()
        {
            _restState = new RestState();
            _refuelState = new RefuelState();
            _accidentState = new AccidentState();
            _speedAdjuster = new SpeedAdjuster();
        }

        /// <summary>
        /// Initializes the SemiTruckDriver with the provided layer.
        /// </summary>
        public void Init(SemiTruckLayer layer)
        {
            // Set up the environment and layer
            _layer = layer;
            _environment = _layer.Environment;
            _unregister = _layer.UnregisterAgent;

            // Create the SemiTruck
            SemiTruck = CreateSemiTruck();
            SemiTruck.Environment = _environment;
            SemiTruck.Layer = _layer;
            SemiTruck.InitializeFuelStrategy();
            SemiTruck.InitializeRestStrategy();
            SemiTruck.ApplyLoad(PayloadKg);
            _fuelTracker = new FuelConsumptionTracker(SemiTruck);
            DefaultAccidentsPerYear = SemiTruck.AccidentsPerYear;
            _accidentState.DefaultAccidentsPerYear = DefaultAccidentsPerYear;

            // Initialize route manager now that we have the truck
            _routeManager = new RouteManager(_layer, _environment, SemiTruck, DriveMode, _unregister);

            // Find initial route
            var route = SemiTruckRouteFinder.Find(_environment, DriveMode, StartLat, StartLon, DestLat, DestLon,
                null, "", SemiTruck.Height, SemiTruck.Mass, SemiTruck.Width, SemiTruck.Length,
                SemiTruck.MaxIncline, _layer.RemovedEdges, false);

            // Set desired lanes for all stops
            if (route != null)
            {
                foreach (var stop in route.Stops)
                {
                    var lanes = stop.Edge.Lanes?.ToList();
                    var desiredLane = lanes?.FirstOrDefault();
                    int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                    stop.DesiredLane = desiredLaneIndex;
                }
            }

            // Abort simulation if no route was found
            if (route == null || route.Count == 0)
            {
                Console.WriteLine($"No valid route found for truck {ID}. Removing from simulation.");
                LogOutcome(TruckTripOutcome.NoRoute);
                _layer.UnregisterAgent?.Invoke(_layer, this);
                return;
            }

            // Insert the SemiTruck into the environment at the starting node
            var node = route.First().Edge.From;
            _environment.Insert(SemiTruck, node);
            SemiTruck.TryEnterDriver(this, out _steeringHandle);
            _steeringHandle.Route = route;

            if (_layer.notifyTrucks)
                _layer.RegisterTruckForRoute(this, _steeringHandle.Route);

            // Register the agent
            layer.RegisterAgent.Invoke(layer, this);

            // log initial state at tick 0 (fuel consumption, physics, and decision data)
            _steeringHandle.LogInitialState();
            _fuelTracker.UpdateConsumption(_steeringHandle, _layer, SemiTruck);
        }

        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            if (_steeringHandle == null || TripOutcome.HasValue)
                return;

            // Handle pauses (early returns)
            if (_restState.HandlePause(_steeringHandle, _layer, this)) return;
            if (_refuelState.HandlePause(_steeringHandle, _layer, SemiTruck, _fuelTracker, this)) return;

            // Handle accidents
            if (_accidentState.HandleOngoing(this, _layer, RemoveFromSimulation)) return;
            if (_accidentState.HandleChance(_steeringHandle, _layer, SemiTruck, _layer.amountOfTrucks)) return;

            if (_fuelTracker.FuelCarrierAmount <= 0)
            {
                FinishTrip(TruckTripOutcome.ZeroEnergy);
                return;
            }
            // Route management
            if (!_layer.notifyTrucks)
            {
                if (!_routeManager.LookaheadAndBypass(_steeringHandle, this))
                    return;
            }

            // Speed adjustments
            if (_steeringHandle.Route.Count > 0)
            {
                var edge = _steeringHandle.Route[0].Edge;
                _speedAdjuster.AdjustForWeather(this, WeatherLayer);
                _speedAdjuster.UpdateOvertaking(edge, this);
                _speedAdjuster.AdjustForIncline(edge, SemiTruck, this, _fuelTracker);
            }

            _restState.ValidateApproach(_steeringHandle, _layer, SemiTruck, _fuelTracker, DrivingTime);
            _refuelState.ValidateApproach(_steeringHandle, _layer, SemiTruck, _fuelTracker, DrivingTime);
            RequestDueRest();
            RequestRestAtCharge();
            if (!_restState.IsActive && !_restState.HasRequest)
                _refuelState.CheckAndPlan(_steeringHandle, _layer, SemiTruck, _fuelTracker, PlanRefuelStop, DrivingTime,
                    CreateStopSearch(_refuelState));

            if (_refuelState.TakeRestAreaFallback() is { } restFallback)
            {
                _restState.Request(restFallback);
            }

            // Plan the requested rest stop
            if (!_refuelState.IsActive)
                _restState.CheckAndPlan(_steeringHandle, _layer, SemiTruck, _fuelTracker, PlanRestStop, DrivingTime,
                    CreateStopSearch(_restState));

            if (_restState.PlanningFailed || _refuelState.PlanningFailed ||
                !DrivingTime.CanDrive(_layer._tickDuration))
            {
                FinishTrip(_refuelState.PlanningFailed && !_refuelState.HasCombinedRestRequest
                    ? TruckTripOutcome.NoReachableFuel : TruckTripOutcome.NoLegalStop);
                return;
            }

            // Move and check goal
            _steeringHandle.StopAtNode = _refuelState.TargetNode ?? _restState.TargetNode;
            _fuelTracker.BeginMovement(_steeringHandle);
            _steeringHandle.Move();
            DrivingTime.AddDriving(_layer._tickDuration);
            _fuelTracker.UpdateConsumption(_steeringHandle, _layer, SemiTruck);
            if (GoalReached)
            {
                FinishTrip(TruckTripOutcome.Completed);
            }
            else if (_fuelTracker.FuelCarrierAmount <= 0 &&
                     !_refuelState.IsAtTarget(_steeringHandle))
            {
                FinishTrip(TruckTripOutcome.ZeroEnergy);
            }
        }

        /// <summary>
        /// Called if a known edge in the truck's route becomes blocked.
        /// </summary>
        public void NotifyEdgeBlocked(ISpatialEdge blockedEdge)
        {
            var upcomingRoute = _steeringHandle.Route.Skip(_steeringHandle.Route.PassedStops);

            if (upcomingRoute.Any(r => r.Edge == blockedEdge))
            {
                _fuelTracker.MarkRouteChanged();
                _routeManager.CreateBypass(blockedEdge, _steeringHandle, this);
            }
        }

        // Private helper methods

        private void RemoveFromSimulation()
        {
            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

            _environment.Remove(SemiTruck);
            _unregister.Invoke(_layer, this);
        }

        private void LogOutcome(TruckTripOutcome outcome)
        {
            TripOutcome = outcome;
            PostgresDbLogger.Instance?.Log(new TruckTripEntity(SemiTruck.ID, _layer.GetCurrentTick(),
                _layer._simulationTime, outcome, FuelCarrierType, FuelCarrierAmount, RemainingRouteDistanceToGoal,
                DrivingTime.DrivingSinceBreak.TotalSeconds,
                DrivingTime.DrivingSinceDailyRest.TotalSeconds, Latitude, Longitude));
        }

        private void FinishTrip(TruckTripOutcome outcome)
        {
            _steeringHandle.Stop();
            LogOutcome(outcome);
            if (outcome != TruckTripOutcome.Completed)
            {
                var details = _restState.PlanningFailed ? _restState.PlanningFailureDetails :
                    _refuelState.PlanningFailureDetails;
                Console.WriteLine($"Truck {ID} could not complete its trip: {outcome}." +
                    (details == null ? "" : $" {details}"));
            }
            RemoveFromSimulation();
        }

        /// <summary>
        /// Asks the rest strategy whether a break or daily rest is due and hands the request to the state that
        /// serves the chosen facility. Only one stop is in flight at a time; a stop that is already under way
        /// is credited towards the break when it ends.
        /// </summary>
        private void RequestDueRest()
        {
            if (_restState.IsActive || _refuelState.IsActive || _restState.HasRequest || _refuelState.HasCombinedRestRequest)
                return;

            var margin = SemiTruck.RestPlanningMarginMinutes > 0
                ? TimeSpan.FromMinutes(SemiTruck.RestPlanningMarginMinutes)
                : SemiTruckDriverConstants.DefaultRestPlanningMargin;
            var requirement = SemiTruck.RestPlanningStrategy.NextRequirement(DrivingTime, SemiTruck, _fuelTracker,
                _steeringHandle.Route.RemainingRouteDistanceToGoal, margin) ?? CoverageRequirement();
            if (requirement == null) return;

            if (requirement.Facility == StopFacility.ChargingStation) _refuelState.RequestCombinedRest(requirement);
            else _restState.Request(requirement);
        }

        /// <summary>
        /// Plans the next required rest before the truck passes the last rest facility it can still legally reach,
        /// so that a sparse corridor ahead cannot leave it without any legal stop.
        /// </summary>
        private RestRequirement CoverageRequirement()
        {
            double remaining = _steeringHandle.Route.RemainingRouteDistanceToGoal;
            var strategy = SemiTruck.RestPlanningStrategy;
            var requirement = strategy.RequirementBeforeDestination(DrivingTime, SemiTruck, _fuelTracker, remaining);
            if (requirement == null) return null;

            long tick = _layer.GetCurrentTick();
            if (!ReferenceEquals(_coverageRoute, _steeringHandle.Route) ||
                tick - _coverageTick >= SemiTruckDriverConstants.RestCoverageRefreshTicks)
            {
                double reach = DrivingTime.LegalDrivingLeft.TotalSeconds *
                    Math.Max(1, SemiTruck.PlanningMaxSpeed * SemiTruckDriverConstants.PlanningSpeedFraction);
                _coverageRemaining = _restState.LastApproachRemaining(_steeringHandle, _layer, reach);
                _coverageRoute = _steeringHandle.Route;
                _coverageTick = tick;
            }
            // Without any facility in reach, keep the regular trigger: the reach may still grow at actual speed.
            return _coverageRemaining is { } last &&
                remaining - last <= SemiTruckDriverConstants.RestCoverageGuardMeters ? requirement : null;
        }

        private Route _coverageRoute;
        private long _coverageTick;
        private double? _coverageRemaining;

        private void RequestRestAtCharge()
        {
            if (_restState.IsActive || _refuelState.IsActive || _restState.HasRequest || _refuelState.HasCombinedRestRequest ||
                !_refuelState.IsChargeDue(_steeringHandle, SemiTruck, _fuelTracker)) return;

            var margin = SemiTruck.RestPlanningMarginMinutes > 0
                ? TimeSpan.FromMinutes(SemiTruck.RestPlanningMarginMinutes)
                : SemiTruckDriverConstants.DefaultRestPlanningMargin;
            var requirement = SemiTruck.RestPlanningStrategy.RequirementAtCharge(DrivingTime, SemiTruck, _fuelTracker,
                _steeringHandle.Route.RemainingRouteDistanceToGoal, margin);
            if (requirement != null) _refuelState.RequestCombinedRest(requirement);
        }

        private void PlanRestStop(double lat, double lon, ISpatialNode insertFromNode)
        {
            _routeManager.PlanRouteWithStop(lat, lon, insertFromNode, _steeringHandle, this, StopType.Rest,
                onSuccess: (restNode) =>
                {
                    _restState.MarkPlanned();
                    _restState.SetTargetNode(restNode);
                    _fuelTracker.MarkRouteChanged();
                },
                onFailure: () => _restState.CancelPlanned()
            );
        }

        private void PlanRefuelStop(double lat, double lon, ISpatialNode insertFromNode)
        {
            _routeManager.PlanRouteWithStop(lat, lon, insertFromNode, _steeringHandle, this, StopType.Refuel,
                onSuccess: (refuelNode) =>
                {
                    _refuelState.MarkPlanned();
                    _refuelState.SetTargetNode(refuelNode);
                    _fuelTracker.MarkRouteChanged();
                },
                onFailure: () => _refuelState.CancelPlanned()
            );
        }

        internal StopRouteSearch CreateStopSearch(StopState state)
        {
            var connectors = new Dictionary<(ISpatialNode Stop, ISpatialNode Rejoin), Route?>();
            var rejections = new Dictionary<StopRouteRejection, int>();
            return new StopRouteSearch(
                (lat, lon, node) => _routeManager.CreateStopRoute(lat, lon, node, _steeringHandle, this, connectors,
                    reason => rejections[reason] = rejections.GetValueOrDefault(reason) + 1),
                plan => _routeManager.ApplyStopRoute(plan, _steeringHandle, this, node =>
                {
                    state.MarkPlanned();
                    state.SetTargetNode(node);
                    _fuelTracker.MarkRouteChanged();
                }),
                () => string.Join(", ", rejections.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        }

        private SemiTruck CreateSemiTruck()
        {
            return _layer.EntityManager.Create<SemiTruck>("type", TruckType);
        }

        // Public properties and interface implementations

        public Guid ID { get; set; }

        public bool GoalReached => _steeringHandle?.GoalReached ?? false;

        public Position Position
        {
            get => SemiTruck.Position;
            set => SemiTruck.Position = value;
        }

        [PropertyDescription] public double StartLat { get; set; }
        [PropertyDescription] public double StartLon { get; set; }
        [PropertyDescription] public double DestLat { get; set; }
        [PropertyDescription] public double DestLon { get; set; }
        [PropertyDescription] public int DriveMode { get; set; }
        [PropertyDescription] public string TruckType { get; set; }

        /// <summary>Optional per-run payload in kg; overrides the truck type's default payload.</summary>
        public double? PayloadKg
        {
            get => Payload >= 0 ? Payload : null;
            set => Payload = value ?? -1;
        }

        /// <summary>Initializer column <c>Payload</c> (kg); negative or absent means "use the truck type default".</summary>
        [PropertyDescription(Name = "Payload")]
        public double Payload { get; set; } = -1;

        public double FuelCarrierAmount
        {
            get => _fuelTracker?.FuelCarrierAmount ?? 0;
            set { if (_fuelTracker != null) _fuelTracker.FuelCarrierAmount = value; }
        }

        public string FuelCarrierType => SemiTruck?.FuelCarrierType.ToString() ?? "Unknown";

        public double Latitude => Position?.Latitude ?? 0.0;

        public double Longitude => Position?.Longitude ?? 0.0;

        public double PositionOnEdge => SemiTruck?.PositionOnCurrentEdge ?? 0.0;

        public void Notify(PassengerMessage passengerMessage)
        {
            throw new NotImplementedException();
        }

        public bool OvertakingActivated { get; set; }

        public bool BrakingActivated { get; set; }

        public Route Route => _steeringHandle?.Route;

        public string NextTrafficLightPhase => _steeringHandle?.NextTrafficLightPhase.ToString() ?? "unknown";

        [PropertyDescription(Name = "velocity")]
        public double Velocity
        {
            get => SemiTruck?.Velocity ?? 0.0;
            set => SemiTruck.Velocity = value;
        }

        public double VelocityInKm => Velocity * 3.6;

        [PropertyDescription(Name = "maxSpeed")]
        public double MaxSpeed
        {
            get => SemiTruck?.MaxSpeed ?? 0.0;
            set => SemiTruck.MaxSpeed = value;
        }

        [PropertyDescription(Name = "speedLimit")]
        public double SpeedLimit => _steeringHandle?.SpeedLimit ?? 0.0;

        public double RemainingDistanceOnEdge => _steeringHandle?.RemainingDistanceOnEdge ?? 0.0;

        [PropertyDescription(Name = "stableId")]
        public string StableId { get; set; }

        public bool CurrentlyCarDriving => true;

        public double RemainingRouteDistanceToGoal => _steeringHandle?.Route?.RemainingRouteDistanceToGoal ?? 0.0;

        public string CurrentEdgeId
        {
            get
            {
                var edge = SemiTruck?.CurrentEdge;
                if (edge == null || !edge.Attributes.ContainsKey("osmid"))
                    return "-1";
                var osmId = edge.Attributes["osmid"].ToString();
                return osmId?.StartsWith("[") == true ? "-1" : osmId ?? "-1";
            }
        }

        [PropertyDescription(Name = "trafficCode", Ignore = true)]
        public string TrafficCode
        {
            get => SemiTruck.TrafficCode;
            set => SemiTruck.TrafficCode = value;
        }
    }
}
