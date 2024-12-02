using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Car.Common;
using SOHModel.Car.Steering;
using SOHModel.Domain.Steering.Common;


namespace SOHModel.Car.Model
{
    public sealed class EmergencyCarDriver : AbstractAgent, ICarSteeringCapable
    {
        private readonly CarDriver _carDriver;

        public EmergencyCarDriver(CarLayer layer, RegisterAgent register, UnregisterAgent unregister, int driveMode,
            double startLat = 0, double startLon = 0, double destLat = 0, double destLon = 0,
            ISpatialEdge startingEdge = null, string osmRoute = "", string trafficCode = "german")
        {
            Layer = layer;
            _environment = layer.Environment;
            _unregister = unregister;

            Car = CreateCar();
            Car.Environment = _environment;
            TrafficCode = trafficCode;

            var route = CarRouteFinder.Find(_environment, driveMode,
                startLat, startLon, destLat, destLon, startingEdge, osmRoute);
            var node = route.First().Edge.From;
            _environment.Insert(Car, node);

            Car.TryEnterDriver(this, out _steeringHandle);
            _steeringHandle.Route = route;

            register.Invoke(layer, this);

            SirenActive = false;
        }

        [PropertyDescription(Name = "sirenActive")]
        public bool SirenActive { get; set; }

        public void ActivateSiren()
        {
            SirenActive = true;
        }

        public void DeactivateSiren()
        {
            SirenActive = false;
        }

        public void Notify(PassengerMessage passengerMessage)
        {
            if (passengerMessage == PassengerMessage.GoalReached)
                _unregister.Invoke(Layer, this);
        }

        private Car CreateCar()
        {
            return Layer.EntityManager.Create<Car>("type", "Golf");
        }

        public override void Tick()
        {
            _steeringHandle.Move();
            if (GoalReached)
            {
                _environment.Remove(Car);
                _unregister.Invoke(Layer, this);
            }
        }
        
        #region fields

        private readonly CarSteeringHandle _steeringHandle;
        private readonly UnregisterAgent _unregister;
        private readonly ISpatialGraphEnvironment _environment;

        #endregion

        #region properties

        private CarLayer Layer { get; }

        public Position Position
        {
            get => Car.Position;
            set => Car.Position = value;
        }

        public Route Route => _steeringHandle.Route;

        public double Latitude => Position.Latitude;

        public double Longitude => Position.Longitude;

        /// <summary>
        ///     Indicates the current light phase (red,green,yellow) of the next traffic light if available.
        /// </summary>
        public string NextTrafficLightPhase => _steeringHandle.NextTrafficLightPhase.ToString();

        [PropertyDescription(Name = "velocity")]
        public double Velocity
        {
            get => Car.Velocity;
            set => Car.Velocity = value;
        }

        public double VelocityInKm => Velocity * 3.6;

        [PropertyDescription(Name = "maxSpeed")]
        public double MaxSpeed
        {
            get => Car.MaxSpeed;
            set => Car.MaxSpeed = value;
        }

        [PropertyDescription(Name = "speedLimit")]
        public double SpeedLimit => _steeringHandle.SpeedLimit;

        public double RemainingDistanceOnEdge => _steeringHandle.RemainingDistanceOnEdge;

        public double PositionOnEdge => Car.PositionOnCurrentEdge;

        [PropertyDescription(Name = "stableId")]
        public string StableId { get; set; }

        public bool GoalReached => _steeringHandle.GoalReached;

        public Car Car { get; set; }

        public bool OvertakingActivated { get; set; }
        public bool BrakingActivated { get; set; }

        public bool CurrentlyCarDriving => true;

        public double RemainingRouteDistanceToGoal => _steeringHandle.Route.RemainingRouteDistanceToGoal;

        public string CurrentEdgeId
        {
            get
            {
                if (Car.CurrentEdge == null || !Car.CurrentEdge.Attributes.ContainsKey("osmid"))
                    return "-1";
                var osmId = Car.CurrentEdge.Attributes["osmid"].ToString();
                return osmId[0] == '[' ? "-1" : osmId;
            }
        }

        /// <summary>
        ///     Get or sets the intersection behaviour model identified by code when no traffic signals are available
        ///     "german" = right before left rule
        ///     "southAfrica" = first in first out (FIFO) rule
        /// </summary>
        [PropertyDescription(Name = "trafficCode", Ignore = true)]
        public string TrafficCode
        {
            get => Car.TrafficCode;
            set => Car.TrafficCode = value;
        }

        #endregion
    }
}
