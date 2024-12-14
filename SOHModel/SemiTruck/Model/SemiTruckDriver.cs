using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.Steering;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents a driver for the SemiTruck, responsible for route planning and managing the truck's movement.
    /// </summary>
    public class SemiTruckDriver : IAgent<SemiTruckLayer>, ISemiTruckSteeringCapable
    {
        // Private fields for managing the SemiTruck's steering and its environment
        private SemiTruckSteeringHandle _steeringHandle;
        private UnregisterAgent _unregister;
        private ISpatialGraphEnvironment _environment;
        private SemiTruckLayer _layer;

        // Public property for the associated SemiTruck
        public SemiTruck SemiTruck { get; set; }

        /// <summary>
        /// Initializes the SemiTruckDriver with the provided layer.
        /// </summary>
        /// <param name="layer">The SemiTruckLayer to which the driver belongs.</param>
        public void Init(SemiTruckLayer layer)
        {
            // Set up the environment and layer 
            _layer = layer;
            _environment = _layer.Environment;
            _unregister = _layer.UnregisterAgent;
            // Create the SemiTruck
            SemiTruck = CreateSemiTruck();
            SemiTruck.Environment = _environment;
            
            //Define SpatialEdge for driveMode 5 as First Outgoing Edge
            ISpatialEdge startingEdge = null;
            // var startNode = _environment.NearestNode(Position.CreateGeoPosition(StartLon, StartLat));
            // startingEdge = startNode.OutgoingEdges.Values.FirstOrDefault();
            var route = SemiTruckRouteFinder.Find(_environment, DriveMode, StartLat,StartLon, DestLat, DestLon, startingEdge, "");
            // Insert the SemiTruck into the environment at the starting node
            
            var node = route.First().Edge.From;
            _environment.Insert(SemiTruck, node);
            SemiTruck.TryEnterDriver(this, out _steeringHandle);
            _steeringHandle.Route = route;
            
            // Register the agent
            layer.RegisterAgent.Invoke(layer, this);
        }

        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            _steeringHandle.Move();
            if (GoalReached)
            {
                _environment.Remove(SemiTruck);
                _unregister.Invoke(_layer, this);
            }
        }


        /// <summary>
        /// Creates a new SemiTruck instance and initializes its steering handle.
        /// </summary>
        /// <returns>A new SemiTruck instance.</returns>
        private SemiTruck CreateSemiTruck()
        {
            return _layer.EntityManager.Create<SemiTruck>("type", TruckType);
        }

        // Unique identifier for the SemiTruckDriver
        public Guid ID { get; set; }

        // Indicates whether the SemiTruck has reached its goal
        public bool GoalReached => _steeringHandle.GoalReached;

        // Current position of the SemiTruckDriver
        public Position Position
        {
            get => SemiTruck.Position;
            set => SemiTruck.Position = value;
        }
        [PropertyDescription]
        public double StartLat { get; set; }
        [PropertyDescription]
        public double StartLon { get; set; }
        [PropertyDescription]
        public double DestLat { get; set; }
        [PropertyDescription]
        public double DestLon { get; set; }
        [PropertyDescription]
        public int DriveMode { get; set; }
        [PropertyDescription]
        public string TruckType { get; set; }
        public double Latitude => Position.Latitude;

        public double Longitude => Position.Longitude;
        
        public double PositionOnEdge => SemiTruck.PositionOnCurrentEdge;

        /// <summary>
        /// Notifies the driver with a message (not implemented yet).
        /// </summary>
        /// <param name="passengerMessage">The message to notify the driver with.</param>
        public void Notify(PassengerMessage passengerMessage)
        {
            throw new NotImplementedException();
        }

        // Indicates whether overtaking is activated (not implemented yet)
        public bool OvertakingActivated { get; }

        // Indicates whether braking is activated
        public bool BrakingActivated { get; set; }
        
        public Route Route => _steeringHandle.Route;
        
        /// <summary>
        ///     Indicates the current light phase (red,green,yellow) of the next traffic light if available.
        /// </summary>
        public string NextTrafficLightPhase => _steeringHandle.NextTrafficLightPhase.ToString();
        
        [PropertyDescription(Name = "velocity")]
        public double Velocity
        {
            get => SemiTruck.Velocity;
            set => SemiTruck.Velocity = value;
        }
        
        public double VelocityInKm => Velocity * 3.6;
        
        
        [PropertyDescription(Name = "maxSpeed")]
        public double MaxSpeed
        {
            get => SemiTruck.MaxSpeed;
            set => SemiTruck.MaxSpeed = value;
        }

        [PropertyDescription(Name = "speedLimit")]
        public double SpeedLimit => _steeringHandle.SpeedLimit;

        public double RemainingDistanceOnEdge => _steeringHandle.RemainingDistanceOnEdge;
        
        [PropertyDescription(Name = "stableId")]
        public string StableId { get; set; }
        
        public bool CurrentlyCarDriving => true;

        public double RemainingRouteDistanceToGoal => _steeringHandle.Route.RemainingRouteDistanceToGoal;

        public string CurrentEdgeId
        {
            get
            {
                if (SemiTruck.CurrentEdge == null || !SemiTruck.CurrentEdge.Attributes.ContainsKey("osmid"))
                    return "-1";
                var osmId = SemiTruck.CurrentEdge.Attributes["osmid"].ToString();
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
            get => SemiTruck.TrafficCode;
            set => SemiTruck.TrafficCode = value;
        }

    }
    
    
    
}
