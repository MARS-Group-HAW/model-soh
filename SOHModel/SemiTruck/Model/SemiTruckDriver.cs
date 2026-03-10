using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.Model.Driver.Services;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.RealTimeData;
using SOHModel.SemiTruck.Steering;
using Position = Mars.Interfaces.Environments.Position;

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
        private readonly FuelConsumptionTracker _fuelTracker;
        private readonly SpeedAdjuster _speedAdjuster;
        private RouteManager _routeManager; // Initialized after truck creation

        // Properties required for initialization
        [PropertyDescription] public SemiTruckWeatherLayer WeatherLayer { get; set; }
        public double DefaultAccidentsPerYear { get; set; }
        public SemiTruck SemiTruck { get; set; }

        // Constructor
        public SemiTruckDriver()
        {
            _restState = new RestState();
            _refuelState = new RefuelState();
            _accidentState = new AccidentState();
            _fuelTracker = new FuelConsumptionTracker();
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
            _fuelTracker.FuelCarrierAmount = SemiTruck.MaxFuelCarrierAmount;
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

            // Initialize rest state
            _restState.Initialize(_layer._simulationTime);
        }

        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            if (_steeringHandle == null)
                return;

            // Handle pauses (early returns)
            if (_restState.HandlePause(_steeringHandle, _layer, this)) return;
            if (_refuelState.HandlePause(_steeringHandle, _layer, SemiTruck, _fuelTracker, this)) return;

            // Handle accidents
            if (_accidentState.HandleOngoing(this, _layer, RemoveFromSimulation)) return;
            if (_accidentState.HandleChance(_steeringHandle, _layer, SemiTruck, _layer.amountOfTrucks)) return;

            // Update fuel and check refueling
            _fuelTracker.UpdateConsumption(_steeringHandle, _layer, SemiTruck);
            _refuelState.CheckAndPlan(_steeringHandle, _layer, SemiTruck, _fuelTracker, PlanRefuelStop);

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

            // Check rest requirements
            _restState.CheckAndPlan(_steeringHandle, _layer, SemiTruck, _fuelTracker, PlanRestStop);

            // Move and check goal
            _steeringHandle.Move();
            if (GoalReached)
            {
                RemoveFromSimulation();
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
