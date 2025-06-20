using Mars.Common.Core;
using Mars.Components.Environments;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Numerics;
using ServiceStack;
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
        private static int truckCounter = 0; // Shared counter across all instances

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

            var route = SemiTruckRouteFinder.Find(_environment, DriveMode, StartLat, StartLon, DestLat, DestLon,
                startingEdge, "", SemiTruck.Height, SemiTruck.Mass, SemiTruck.Width, SemiTruck.Length,
                SemiTruck.MaxIncline, _layer.RemovedEdges);


            //Possible Output two see progress of Routing calculation, currently prints every 100 created Routes
            // truckCounter++;
            // if (truckCounter % 100 == 0)
            // {
            //     Console.WriteLine($"Created routes for {truckCounter} trucks...");
            // }

            if (route == null || route.Count == 0)
            {
                Console.WriteLine($"No valid route found for truck {ID}. Removing from simulation.");
                _layer.UnregisterAgent?.Invoke(_layer, this); // Remove agent from simulation
                return;
            }

            // Insert the SemiTruck into the environment at the starting node
            var node = route.First().Edge.From;
            _environment.Insert(SemiTruck, node);
            SemiTruck.TryEnterDriver(this, out _steeringHandle);
            _steeringHandle.Route = route;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(this, _steeringHandle.Route);
            // Register the agent
            layer.RegisterAgent.Invoke(layer, this);
        }

        private int _lastCheckedRemovedEdgesVersion = -1;

        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            if (_steeringHandle == null)
            {
                return;
            }

            if (!_layer.notifyTrucks)
            {
                // Check if the next edge in the route is still available
                if (_steeringHandle.Route.Count > 0)
                {
                    double distanceAhead = 0;
                    for (int i = 0; i < _steeringHandle.Route.Count; i++)
                    {
                        var edge = _steeringHandle.Route[i].Edge;
                        distanceAhead += edge.Length;
                        // If we find a removed edge within 5km, trigger recalculation
                        if (_layer.RemovedEdges.Contains(edge))
                        {
                            if (!CreateBypass(edge)) return; // If no alternative found, stop execution
                            break;
                        }

                        // Stop checking after 5km
                        if (distanceAhead >= 5000)
                            break;
                    }
                }
            }


            _steeringHandle.Move();
            if (GoalReached)
            {
                if (_layer.notifyTrucks) _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);
                _environment.Remove(SemiTruck);
                _unregister.Invoke(_layer, this);
            }
        }

        /// <summary>
        /// Called if a known edge in the truck's route becomes blocked.
        /// Triggers rerouting if affected.
        /// </summary>
        public void NotifyEdgeBlocked(ISpatialEdge blockedEdge)
        {
            if (_steeringHandle.Route.Any(r => r.Edge == blockedEdge))
            {
                CreateBypass(blockedEdge);
            }
        }


        /// <summary>
        /// Attempts to calculate and apply a bypass route for the SemiTruck when a removed edge is encountered.
        /// Finds the next valid edge in the route, recalculates the path, and updates the truck's route accordingly.
        /// If no valid bypass is found, the truck is removed from the simulation.
        /// </summary>
        /// <param name="removedEdge">The edge that has been removed and triggered the rerouting process.</param>
        /// <returns>True if a bypass was successfully created and applied; otherwise, false.</returns>
        private bool CreateBypass(ISpatialEdge removedEdge)
        {
            if (_layer.notifyTrucks) _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);
            // Find first valid EdgeStop AFTER the removed section
            var nextValidEdgeStop = _steeringHandle.Route.Stops
                .Skip(_steeringHandle.Route.PassedStops) // Start from current position
                .SkipWhile(stop => stop.Edge != removedEdge)
                .SkipWhile(stop => _layer.RemovedEdges.Contains(stop.Edge)) // Skip all removed edges
                .FirstOrDefault(stop => !_layer.RemovedEdges.Contains(stop.Edge)); // Find the first valid edge


            if (nextValidEdgeStop == null)
            {
                Console.WriteLine($"No valid EdgeStop found after removed edges! Stopping truck.");
                _unregister.Invoke(_layer, this); // Remove truck from simulation
                return false;
            }

            // Calculate bypass route
            var bypassRoute = SemiTruckRouteFinder.Find(
                _environment, DriveMode, _steeringHandle.Position.Latitude,
                _steeringHandle.Position.Longitude,
                nextValidEdgeStop.Edge.From.Position.Latitude, nextValidEdgeStop.Edge.From.Position.Longitude,
                null, "", SemiTruck.Height, SemiTruck.Mass,
                SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline, _layer.RemovedEdges
            );

            if (bypassRoute == null || bypassRoute.Count == 0)
            {
                Console.WriteLine($"No alternative route found! Stopping truck.");
                _unregister.Invoke(_layer, this); // Remove truck from simulation
                return false;
            }

            // While the current EdgeStop is invalid, keep removing
            int removeIndex = _steeringHandle.Route.PassedStops;
            while (_steeringHandle.Route.Stops.Count > 0 &&
                   !_steeringHandle.Route.Stops[removeIndex].Equals(nextValidEdgeStop))
            {
                _steeringHandle.Route.Stops.RemoveAt(removeIndex);
            }

            foreach (var edgeStop in _steeringHandle.Route)
            {
                bypassRoute.Add(edgeStop.Edge);
            }


            _steeringHandle.Route = bypassRoute;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(this, _steeringHandle.Route);

            return true;
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
        public bool GoalReached => _steeringHandle?.GoalReached ?? false;


        // Current position of the SemiTruckDriver
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
        public double Latitude => Position?.Latitude ?? 0.0;

        public double Longitude => Position?.Longitude ?? 0.0;

        public double PositionOnEdge => SemiTruck?.PositionOnCurrentEdge ?? 0.0;

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


        public Route Route => _steeringHandle?.Route;

        /// <summary>
        ///     Indicates the current light phase (red,green,yellow) of the next traffic light if available.
        /// </summary>
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