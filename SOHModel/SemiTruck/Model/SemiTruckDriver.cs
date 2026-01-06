using System.Drawing.Printing;
using Mars.Common.Core;
using Mars.Components.Environments;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using Mars.Numerics;
using NetTopologySuite.GeometriesGraph;
using ServiceStack;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;
using SOHModel.SemiTruck.RealTimeData;
using SOHModel.SemiTruck.Steering;
using Edge = NetTopologySuite.Planargraph.Edge;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents an autonomous agent that simulates a semi-truck driver in a spatial simulation.
    /// Responsible for steering, refueling, resting, accident handling, and dynamic route planning.
    /// </summary>
    public class SemiTruckDriver : IAgent<SemiTruckLayer>, ISemiTruckSteeringCapable
    {
        // Private fields for managing the SemiTruck's steering and its environment
        private SemiTruckSteeringHandle _steeringHandle; // Controls the vehicle's path and motion
        private UnregisterAgent _unregister; // Delegate to remove agent from simulation
        private ISpatialGraphEnvironment _environment; // Spatial graph (e.g., roads) in which agent moves
        private SemiTruckLayer _layer; // Reference to the current simulation layer

        private static int truckCounter = 0; // Counter for diagnostic purposes
        private readonly Random _random = new Random(); // For probabilistic events like accidents

        private bool _hasAccident = false; // Whether an accident is currently active
        private int _accidentTicksRemaining = 0; // Ticks remaining to resolve the accident

        private DateTime _lastBreakTime; // Last time a rest pause was taken
        private DateTime _pausedUntilTime = DateTime.MinValue; // End time of current rest
        private DateTime _refuelUntilTime = DateTime.MinValue; // End time of current refueling

        private bool _restAreaPlanned = false; // If a rest stop is currently scheduled
        private bool _goingToRestArea = false; // Actively heading to rest area
        private bool _pauseCompleted = false; // Rest pause has completed

        private bool _refuelPlanned = false; // If a refuel stop is currently planned
        private bool _goingToRefuel = false; // Heading to refueling location
        private bool _isRefueling = false; // Truck is currently being refueled

        private double _EnergyLevel; // Current energy level
        private double _lastRemainingDistanceToGoal = -1; // For tracking fuel usage
        private bool _routeChanged = false; // Whether route was modified (due to detours, etc.)

        private double _originalMaxSpeed = -1; // Stored value for resetting speed after incline/weather
        private TimeSpan _maxDrivingTimeWithoutBreak = TimeSpan.FromHours(9); // Legal driving limit

        private ISpatialNode _restNode; // Target rest location
        private ISpatialNode _refuelNode = null; // Target refueling node

        private int _lastCheckedRemovedEdgesVersion = -1; // For detecting if edge list changed
        private Route _originalRoute; // Stores route before detour (e.g., for pause)
        private Route _postPauseReturnRoute; // Return logic


        [PropertyDescription] public SemiTruckWeatherLayer WeatherLayer { get; set; }

        public double DefaultAccidentsPerYear { get; set; }


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
            _EnergyLevel = SemiTruck.EnergyAmount;
            DefaultAccidentsPerYear = SemiTruck.AccidentsPerYear;


            //Define SpatialEdge for driveMode 5 as First Outgoing Edge
            ISpatialEdge startingEdge = null;

            var route = SemiTruckRouteFinder.Find(_environment, DriveMode, StartLat, StartLon, DestLat, DestLon,
                startingEdge, "", SemiTruck.Height, SemiTruck.Mass, SemiTruck.Width, SemiTruck.Length,
                SemiTruck.MaxIncline, _layer.RemovedEdges, false);
            
            foreach (var stop in route.Stops)
            {
                List<ISpatialLane> lanes = stop.Edge.Lanes?.ToList();
                var desiredLane = lanes?.FirstOrDefault();
                int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                stop.DesiredLane = desiredLaneIndex;
            }


            //Possible Output two see progress of Routing calculation, currently prints every 100 created Routes
            // truckCounter++;
            // if (truckCounter % 100 == 0)
            // {
            //     Console.WriteLine($"Created routes for {truckCounter} trucks...");
            // }

            // Abort simulation if no route was found
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
            _lastBreakTime = _layer._simulationTime;
        }


        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            if (_steeringHandle == null)
                return;

            // Handle rest and refueling pauses (interrupts simulation tick)
            if (HandleRestPause()) return;
            if (HandleRefuelPause()) return;

            // Handle ongoing accident – truck is stopped during this period
            if (HandleOngoingAccident())
                return;

            // Randomly determine whether an accident occurs
            if (HandleAccidentChance())
                return;

            UpdateFuelConsumption();
            CheckIfRefuelIsRequired();

            // If truck is not being guided externally, check for detours
            if (!_layer.notifyTrucks)
            {
                if (!LookaheadAndBypassIfNeeded())
                    return;
            }

            // Apply rules for weather, overtaking, incline
            if (_steeringHandle.Route.Count > 0)
            {
                var currentEdge = _steeringHandle.Route[0].Edge;
                AdjustSpeedBasedOnWeather();
                UpdateOvertakingPermission(currentEdge);
                AdjustSpeedBasedOnIncline(currentEdge);
            }
            

            // Schedule a rest if needed
            if ((_layer._simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                Route.RemainingRouteDistanceToGoal > 100_000)
            {
                CheckAndMoveToRestArea();
                _routeChanged = true;
            }

            // Move the truck along the route
            _steeringHandle.Move();

            // If destination reached, remove agent from simulation
            if (GoalReached)
            {
                if (_layer.notifyTrucks)
                    _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

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
            var upcomingRoute = _steeringHandle.Route.Skip(_steeringHandle.Route.PassedStops);

            if (upcomingRoute.Any(r => r.Edge == blockedEdge))
            {
                _routeChanged = true;
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
            
            var stops = _steeringHandle.Route.Stops;
            int passed = _steeringHandle.Route.PassedStops;
            if (stops == null || stops.Count == 0 || passed >= stops.Count)
            {
                Console.WriteLine("Route has no remaining stops. Stopping truck.");
                _unregister.Invoke(_layer, this);
                return false;
            }
            
            // Find first valid EdgeStop AFTER the removed section
            var candidateStops  = _steeringHandle.Route.Stops
                .Skip(_steeringHandle.Route.PassedStops) // Start from current position
                .SkipWhile(stop => stop.Edge != removedEdge)
                .SkipWhile(stop => _layer.RemovedEdges.Contains(stop.Edge)) // Skip all removed edges
                .Where(stop => !_layer.RemovedEdges.Contains(stop.Edge));

            Route? bypassRoute = null;
            var nextValidEdgeStop = (EdgeStop?)null;
            
            

            foreach (var candidate in candidateStops)
            {
                bypassRoute = SemiTruckRouteFinder.Find(
                    _environment, DriveMode, _steeringHandle.Position.Latitude,
                    _steeringHandle.Position.Longitude,
                    candidate.Edge.From.Position.Latitude, candidate.Edge.From.Position.Longitude,
                    null, "", SemiTruck.Height, SemiTruck.Mass,
                    SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline, _layer.RemovedEdges, true
                );

                // Bei isByPassRoute==true liefert Find keine Partial-Route (null wenn nur partial möglich)
                if (bypassRoute != null && bypassRoute.Count > 0)
                {
                    nextValidEdgeStop = candidate;
                    break;
                }
            }

            if (nextValidEdgeStop == null || bypassRoute == null || bypassRoute.Count == 0)
            {
                Console.WriteLine($"No alternative bypass route found up to destination. Stopping truck.");
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

            //Check if byPass already leads to goal?
            var originalLastStop = _steeringHandle.Route.Stops.LastOrDefault();
            bool bypassEndsAtGoal =
                originalLastStop != null &&
                bypassRoute.LastOrDefault()?.Edge?.To?.Equals(originalLastStop.Edge.To) == true;

            if (!bypassEndsAtGoal)
            {
                // Append the remaining part of the original route (after bypass) to the new route
                foreach (var edgeStop in _steeringHandle.Route)
                {
                    List<ISpatialLane> lanes = edgeStop.Edge.Lanes?.ToList();
                    var desiredLane = lanes?.FirstOrDefault();
                    int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                    // Add each stop from the old route to the bypass route to complete it
                    bypassRoute.Add(edgeStop.Edge, desiredLaneIndex);
                }
            }

            // Assign the final composed route to the truck
            _steeringHandle.Route = bypassRoute;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(this, _steeringHandle.Route);

            return true;
        }
        /// <summary>
        /// Calculates the maximum feasible speed on a given incline, based on vehicle mass and engine power.
        /// Prevents trucks from exceeding physical limits on steep ascents.
        /// </summary>
        /// <param name="inclinePercent">Incline of the edge in percent</param>
        /// <returns>Adjusted speed in m/s</returns>
        double CalculateMaxSpeedOnIncline(double inclinePercent)
        {
            double powerWatt = SemiTruck.Power * 1000.0; // Convert kW to W
            double massKg = SemiTruck.Mass * 1000.0; // Convert t to kg
            double g = 9.81; // Gravity constant
            const double minSpeedMps = 30.0 / 3.6; // Minimum speed: 30 km/h → m/s

            if (inclinePercent <= 0.0)
                return MaxSpeed; // no incline → full speed

            double denominator = massKg * g * (inclinePercent / 100.0);
            if (denominator == 0) return SemiTruck.MaxSpeed;

            double vMps = powerWatt / denominator;
            
            // Clamp result between minimum required speed and current truck max
            return Math.Max(minSpeedMps, Math.Min(vMps, MaxSpeed));
        }

        /// <summary>
        /// Parses an incline string attribute from OSM format (e.g., "5%", "up") into a numerical percentage.
        /// </summary>
        /// <param name="inclineStr">Incline value as string</param>
        /// <returns>Positive incline percentage</returns>
        private double ParseIncline(string inclineStr)
        {
            if (string.IsNullOrWhiteSpace(inclineStr))
                return 0.0;

            inclineStr = inclineStr.Trim().ToLower();

            if (inclineStr.EndsWith("%") && double.TryParse(inclineStr.TrimEnd('%'), out double percent))
                return Math.Abs(percent);

            if (inclineStr == "up") return 5.0;
            if (inclineStr == "down") return 0.0;
            if (double.TryParse(inclineStr, out double value)) return Math.Abs(value);

            return 0.0;
        }

        /// <summary>
        /// Determines whether a random accident occurs based on accident rate and simulation tick duration.
        /// If an accident happens, the truck is stopped and a recovery time is applied.
        /// </summary>
        /// <returns>True if an accident occurred this tick</returns>
        private bool HandleAccidentChance()
        {
            // Adjust accident rate based on total number of trucks in simulation
            double scaledAccidentsPerYear = SemiTruck.AccidentsPerYear * (_layer.amountOfTrucks / 650000.0);
            double secondsPerYear = 365.0 * 24 * 60 * 60;
            double ticksPerYear = secondsPerYear / _layer._tickDuration.TotalSeconds;
            double accidentChancePerTick = scaledAccidentsPerYear / ticksPerYear;

            // Random draw for accident occurrence
            if (_random.NextDouble() < accidentChancePerTick)
            {
                // Default roadside blocking time (average respomd time of ADAC)
                TimeSpan accidentDuration = TimeSpan.FromMinutes(41);

                // Reduce accident duration if shoulder is available
                if (_steeringHandle.Route.Count > 0)
                {
                    var currentEdge = _steeringHandle.Route[0].Edge;
                    if (currentEdge.Attributes.TryGetValue("shoulder", out var shoulderValue))
                    {
                        var shoulderStr = shoulderValue?.ToString()?.ToLower();
                        if (shoulderStr == "yes" || shoulderStr == "both" || shoulderStr == "left" ||
                            shoulderStr == "right")
                        {
                            accidentDuration = TimeSpan.FromMinutes(2); // Pull over instead of blocking road
                        }
                    }
                }

                _hasAccident = true;
                _accidentTicksRemaining = (int)(accidentDuration.TotalSeconds / _layer._tickDuration.TotalSeconds);
                _steeringHandle.Stop();

                Console.WriteLine(
                    $"Truck {SemiTruck.GetId()} had an accident. Time till Road was unblocked: {accidentDuration.TotalMinutes} minutes.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Modifies max speed of the truck depending on incline of current road segment.
        /// This simulates reduced performance on steep hills.
        /// </summary>
        /// <param name="currentEdge">Current road edge</param>
        private void AdjustSpeedBasedOnIncline(ISpatialEdge currentEdge)
        {
            if (currentEdge.Attributes.TryGetValue("incline", out var inclineValue))
            {
                double incline = ParseIncline(inclineValue?.ToString());
                double adjustedSpeed = CalculateMaxSpeedOnIncline(incline);

                // Backup original speed once
                if (_originalMaxSpeed < 0)
                    _originalMaxSpeed = SemiTruck.MaxSpeed;

                // Reduce speed if necessary
                if (adjustedSpeed < _originalMaxSpeed)
                {
                    MaxSpeed = adjustedSpeed;
                }
                else
                {
                    // Reset speed if incline is not present
                    MaxSpeed = _originalMaxSpeed;
                }
            }
            else
            {
                // Keine Steigung → zurücksetzen
                if (_originalMaxSpeed > 0)
                {
                    MaxSpeed = _originalMaxSpeed;
                }
            }
        }

        /// <summary>
        /// Continues ticking down the accident time.
        /// Once the accident ends, the truck is removed from the simulation.
        /// </summary>
        /// <returns>True if truck is still in accident</returns>
        private bool HandleOngoingAccident()
        {
            if (!_hasAccident)
                return false;

            if (_accidentTicksRemaining > 0)
            {
                _accidentTicksRemaining--;
                return true; // Still blocked → skip other logic
            }

            // End accident → remove truck
            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

            _environment.Remove(SemiTruck);
            _unregister.Invoke(_layer, this);

            return true;
        }

        /// <summary>
        /// Looks ahead a few kilometers (currently 5km) into the route and checks for removed (blocked) edges.
        /// If a removed edge is found, a bypass is created.
        /// </summary>
        /// <returns>True if route is still valid or successfully updated, false if rerouting failed</returns>
        private bool LookaheadAndBypassIfNeeded()
        {
            if (_steeringHandle.Route.Count == 0)
                return true; 

            double distanceAhead = 0;

            for (int i = 0; i < _steeringHandle.Route.Count; i++)
            {
                var edge = _steeringHandle.Route[i].Edge;
                distanceAhead += edge.Length;

                if (_layer.RemovedEdges.Contains(edge))
                {
                    _routeChanged = true;
                    return CreateBypass(edge); 
                }

                if (distanceAhead >= 5000)
                    break;
            }

            return true; 
        }

        /// <summary>
        /// Checks the current road segment for overtaking permission and updates agent behavior accordingly.
        /// </summary>
        /// <param name="currentEdge">Current road segment</param>
        private void UpdateOvertakingPermission(ISpatialEdge currentEdge)
        {
            if (currentEdge.Attributes.TryGetValue("overtaking", out var overtakingValue))
            {
                OvertakingActivated = overtakingValue?.ToString()?.ToLower() == "yes";
            }
        }

        /// <summary>
        /// Checks whether the truck must take a mandatory break (e.g., after 9 hours of driving).
        /// If required, it tries to locate a nearby rest area (from OpenStreetMap or CSV fallback),
        /// and re-routes the truck there temporarily.
        /// </summary>
        private void CheckAndMoveToRestArea()
        {
            // Skip if a rest area is already being approached or pause is scheduled
            if (_restAreaPlanned || _goingToRestArea || _pausedUntilTime > _layer._simulationTime)
                return;

            // Enforce rest only if max driving time exceeded and significant trip length remains
            if ((_layer._simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                Route.RemainingRouteDistanceToGoal > 100_000)
            {
                double accumulatedDistance = 0;

                // Search next 100 km for a rest area directly along the route
                foreach (var routeStep in _steeringHandle.Route)
                {
                    var edge = routeStep.Edge;
                    accumulatedDistance += edge.Length;
                    if (accumulatedDistance > 100_000)
                        break;

                    var nodesToCheck = new[] { edge.From, edge.To };

                    foreach (ISpatialNode node in nodesToCheck)
                    {
                        foreach (var connectedEdge in node.OutgoingEdges.Values)
                        {
                            if (connectedEdge.Attributes.TryGetValue("source_tag", out var tag))
                            {
                                var tagStr = tag?.ToString()?.ToLowerInvariant();
                                if (tagStr == "rest_area" || tagStr == "services")
                                {
                                    // Console.WriteLine(
                                    //     $"Rest area or service found nearby – edge ID: {connectedEdge.GetId()}, tag: {tagStr}");


                                    var restCoord = connectedEdge.To.Position;
                                    PlanRouteWithStop(restCoord.Latitude, restCoord.Longitude, node, StopType.Rest);
                                    return;
                                }
                            }
                        }
                    }
                }
                // No nearby OSM rest area found → fallback to predefined list (e.g., from CSV)
                // Console.WriteLine("No rest area within next 100 km. Searching external list...");
                FindNearestRestAreaFromList();
            }
        }
        
        /// <summary>
        /// Handles the pause logic when the truck is resting (e.g., mandatory 4 hours).
        /// Controls timing, transition states, and prevents movement during pause.
        /// </summary>
        /// <returns>True if truck is currently resting and simulation tick should be skipped</returns>
        private bool HandleRestPause()
        {
            // Truck is still in rest pause
            if (_pausedUntilTime > _layer._simulationTime)
                return true; // Tick soll abbrechen

            // Rest time is over → clean up state and continue driving
            if (_pauseCompleted && _pausedUntilTime <= _layer._simulationTime)
            {
                Console.WriteLine("Rest pause completed. Resuming original route.");
                _restAreaPlanned = false;
                _pauseCompleted = false;
                _restNode = null;
            }

            // Truck just arrived at the rest area → begin pause
            if (_goingToRestArea &&
                _steeringHandle.Position != null &&
                _restNode != null &&
                IsOnNode(_restNode))
            {
                Console.WriteLine("Arrived at rest area. Starting pause.");
                _pausedUntilTime = _layer._simulationTime + TimeSpan.FromHours(4); // Legal pause duration
                _lastBreakTime = _layer._simulationTime;
                _goingToRestArea = false;
                _pauseCompleted = true;
                return true; // Tick soll abbrechen
            }

            return false; // Weiter im Tick
        }

        
        /// <summary>
        /// Injects a temporary stop (rest or refuel) into the current route.
        /// This includes three steps: split route → go to stop → return and continue.
        /// </summary>
        /// <param name="targetLat">Latitude of the stop location</param>
        /// <param name="targetLon">Longitude of the stop location</param>
        /// <param name="insertFromNode">The node at which to divert the route</param>
        /// <param name="stopType">Type of stop: Rest or Refuel</param>
        private void PlanRouteWithStop(double targetLat, double targetLon, ISpatialNode insertFromNode,
            StopType stopType)
        {
            // Set flags based on stop type
            if (stopType == StopType.Rest)
            {
                _restAreaPlanned = true;
                _goingToRestArea = true;
            }
            else if (stopType == StopType.Refuel)
            {
                _refuelPlanned = true;
                _goingToRefuel = true;
            }

            // Unregister from current route tracking if applicable
            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

            // Backup current route
            _originalRoute = new Route();
            foreach (var stop in _steeringHandle.Route)
                _originalRoute.Add(stop.Edge, stop.DesiredLane);

            // Identify position to split the route
            var insertIndex = _steeringHandle.Route.Stops
                .Skip(_steeringHandle.Route.PassedStops)
                .ToList()
                .FindIndex(stop => stop.Edge.From == insertFromNode);

            if (insertIndex == -1)
            {
                Console.WriteLine("Could not find valid insertion point for detour.");
                if (stopType == StopType.Rest)
                {
                    _goingToRestArea = false;
                    _restAreaPlanned = false;
                }
                else
                {
                    _goingToRefuel = false;
                    _refuelPlanned = false;
                }

                return;
            }

            var splitStop = _steeringHandle.Route.Stops[_steeringHandle.Route.PassedStops + insertIndex];
            var splitNode = (splitStop.Edge.From == insertFromNode) ? splitStop.Edge.From : splitStop.Edge.To;

            // Build route to stop location
            var toStopRoute = SemiTruckRouteFinder.Find(
                _environment, DriveMode,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                targetLat, targetLon,
                null, "", SemiTruck.Height, SemiTruck.Mass,
                SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline,
                _layer.RemovedEdges, false
            );

            // Remember destination node
            if (stopType == StopType.Rest)
                _restNode = toStopRoute.Stops[^1].Edge.To;

            if (stopType == StopType.Refuel)
                _refuelNode = toStopRoute.Stops[^1].Edge.To;

            // Build return route from stop back to original path
            var backRoute = SemiTruckRouteFinder.Find(
                _environment, DriveMode,
                targetLat, targetLon,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                null, "", SemiTruck.Height, SemiTruck.Mass,
                SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline,
                _layer.RemovedEdges, false
            );

            if (toStopRoute == null || toStopRoute.Count == 0 || backRoute == null || backRoute.Count == 0)
            {
                Console.WriteLine("Failed to compute detour or return route.");
                if (stopType == StopType.Rest)
                {
                    _goingToRestArea = false;
                    _restAreaPlanned = false;
                }
                else
                {
                    _goingToRefuel = false;
                    _refuelPlanned = false;
                }

                return;
            }

            // Merge routes: pre-stop Route + detour (Gas Station / Rest Area) + return-detour + remaining Route
            var newRoute = new Route();

            
            for (int i = _steeringHandle.Route.PassedStops; i < _steeringHandle.Route.PassedStops + insertIndex; i++)
                newRoute.Add(_steeringHandle.Route.Stops[i].Edge, _steeringHandle.Route.Stops[i].DesiredLane);

            foreach (var stop in toStopRoute)
                newRoute.Add(stop.Edge, stop.DesiredLane);

            foreach (var stop in backRoute)
                newRoute.Add(stop.Edge, stop.DesiredLane);

            for (int i = _steeringHandle.Route.PassedStops + insertIndex;
                 i < _steeringHandle.Route.Stops.Count;
                 i++)
                newRoute.Add(_steeringHandle.Route.Stops[i].Edge, _steeringHandle.Route.Stops[i].DesiredLane);

            _steeringHandle.Route = newRoute;

            if (_layer.notifyTrucks)
                _layer.RegisterTruckForRoute(this, _steeringHandle.Route);

            Console.WriteLine($"Truck diverts to stop ({stopType}) at ({targetLat}, {targetLon}) and continues.");
        }

        /// <summary>
        /// Calculates and updates the fuel consumption of the truck based on the distance driven since the last tick.
        /// If the fuel tank reaches zero, the truck will effectively stall until refueled.
        /// </summary>
        private void UpdateFuelConsumption()
        {
            double currentRemainingDistance = _steeringHandle.Route.RemainingRouteDistanceToGoal;
            double distanceDrivenKm = 0.0;

            // If the route changed, skip distance calculation to avoid negative values
            if (_routeChanged || _lastRemainingDistanceToGoal < 0)
            {
                _routeChanged = false;
            }
            else
            {
                // Calculate how far the truck moved (in km)
                distanceDrivenKm = (_lastRemainingDistanceToGoal - currentRemainingDistance) / 1000.0;
                if (distanceDrivenKm < 0) distanceDrivenKm = 0;

                // Calculate energy usage and reduce level
                double energyUsed = (SemiTruck.EnergyConsumptionPer100Km / 100.0) * distanceDrivenKm;
                _EnergyLevel -= energyUsed;

                if (_EnergyLevel <= 0)
                {
                    _EnergyLevel = 0;
                    // Truck has no energy left; will stop moving until refueled
                    //TODO What should happen when a truck runs out of energy?
                }
            }

            _lastRemainingDistanceToGoal = currentRemainingDistance;
        }

        /// <summary>
        /// Checks whether the truck must refuel soon based on current tank level and remaining route length.
        /// If needed, it searches for nearby refuel stations along the route and plans a detour.
        /// </summary>
        private void CheckIfRefuelIsRequired()
        {
            // Skip if already on a refueling mission
            if (_goingToRefuel || _refuelPlanned) return;

            // Estimate how far the truck can go with current energy (in km)
            double availableRangeKm = (_EnergyLevel / SemiTruck.EnergyConsumptionPer100Km) * 100;

            // If range is too low, prepare refuel plan
            if (availableRangeKm < 100)
            {
                // Only search if there's still a long distance to go
                if (Route.RemainingRouteDistanceToGoal > 100_000)
                {
                    double accumulatedDistance = 0;
                    Console.WriteLine($"[Truck {SemiTruck.ID}] Energy low: {availableRangeKm:F1} km remaining – searching for refuel station...");
                    // Scan the next 100 km along the route for gas station tags
                    foreach (var routeStep in _steeringHandle.Route)
                    {
                        var edge = routeStep.Edge;
                        accumulatedDistance += edge.Length;
                        if (accumulatedDistance > 100_000)
                            break;

                        var nodesToCheck = new[] { edge.From, edge.To };

                        foreach (ISpatialNode node in nodesToCheck)
                        {
                            foreach (var connectedEdge in node.OutgoingEdges.Values)
                            {
                                if (connectedEdge.Attributes.TryGetValue("source_tag", out var tag))
                                {
                                    var tagStr = tag?.ToString()?.ToLowerInvariant();
                                    if (tagStr == "services" || tagStr == "fuel" || tagStr == "charging_station")
                                    {
                                        var serviceCoord = connectedEdge.To.Position;
                                        var insertFromNode = connectedEdge.From;

                                        // Plan a detour to the refuel station
                                        PlanRouteWithStop(serviceCoord.Latitude, serviceCoord.Longitude, insertFromNode,
                                            StopType.Refuel);
                                        _refuelPlanned = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    // No refuel station found on the way – fallback to external list
                    Console.WriteLine($"[Truck {SemiTruck.ID}] No refuel station found along route – fallback to CSV list.");
                    FindNearestRefuelStationFromList();
                    _refuelPlanned = true;
                }
            }
        }

        /// <summary>
        /// Controls the behavior of the truck during the refueling process, including start and end of the pause.
        /// Prevents driving while refueling.
        /// </summary>
        /// <returns>True if refueling is ongoing and tick should pause</returns>
        private bool HandleRefuelPause()
        {
            // Truck is currently in refueling phase
            if (_isRefueling && _refuelUntilTime > _layer._simulationTime)
                return true; 

            // Refueling just completed
            if (_isRefueling && _refuelUntilTime <= _layer._simulationTime)
            {
                Console.WriteLine($"[Truck {SemiTruck.ID}] Refueling/Recharging completed – continuing trip.");
                _EnergyLevel = SemiTruck.EnergyAmount; // Reset to full
                _isRefueling = false;
                _refuelPlanned = false;
                _refuelNode = null;
            }

            // Truck has arrived at the fueling point
            if (_goingToRefuel &&
                _steeringHandle.Position != null &&
                _refuelNode != null &&
                IsOnNode(_refuelNode))
            {
                Console.WriteLine($"[Truck {SemiTruck.ID}] Refuel station reached. Starting pause ({SemiTruck.RefuelTimeInMinutes} min).");
                _refuelUntilTime = _layer._simulationTime + TimeSpan.FromMinutes(SemiTruck.RefuelTimeInMinutes);
                _isRefueling = true;
                _goingToRefuel = false;
                return true; // Stop tick
            }

            return false;
        }

        /// <summary>
        /// Adjusts the truck’s max speed and accident risk based on current weather zone.
        /// Speed is reduced in snow, rain or fog, and accident probability may increase.
        /// </summary>
        private void AdjustSpeedBasedOnWeather()
        {
            if (WeatherLayer == null || Position == null)
                return;

            var point = new NetTopologySuite.Geometries.Point(Longitude, Latitude);
            var now = WeatherLayer.SemiTruckLayer.Context.CurrentTimePoint ?? DateTime.Now;
            // Find weather zone that contains the truck's current position
            var affectedZone = WeatherLayer.AllZones
                .FirstOrDefault(z =>
                    z.Area.Contains(point) &&
                    z.SpeedFactor < 1.0 &&
                    z.StartTime <= now &&
                    z.EndTime >= now);

            // Store original speed once for reset
            if (_originalMaxSpeed < 0)
                _originalMaxSpeed = SemiTruck.MaxSpeed;

            // Reset accident rate (in case previously modified)
            SemiTruck.AccidentsPerYear = DefaultAccidentsPerYear;

            if (affectedZone != null)
            {
                MaxSpeed = _originalMaxSpeed * affectedZone.SpeedFactor;
                
                // Adjust accident risk if conditions are snowy or severely slowed
                if (affectedZone.Type?.ToLower().Contains("schnee") == true ||
                    affectedZone.SpeedFactor <= 0.6) 
                {
                    SemiTruck.AccidentsPerYear *= 2.06;
                }
            }
            else
            {
                MaxSpeed = _originalMaxSpeed;
            }
        }

        /// <summary>
        /// Searches for the nearest rest area from the external list (e.g., CSV) based on current position.
        /// Used as fallback when no rest area was found on the active route.
        /// </summary>
        private void FindNearestRestAreaFromList()
        {
            var currentLat = _steeringHandle.Position.Latitude;
            var currentLon = _steeringHandle.Position.Longitude;

            var nearest = _layer.AllRestAreas.MinBy(r => GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));
            if (nearest != null)
            {
                Console.WriteLine($"Using fallback rest area from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                PlanRouteWithStop(nearest.Lat, nearest.Lon, Route.Stops[_steeringHandle.Route.PassedStops].Edge.To,
                    StopType.Rest);
            }
            else
            {
                Console.WriteLine("No fallback rest area found in external list.");
            }
        }

        /// <summary>
        /// Searches for the nearest refuel station from the external list (e.g., CSV).
        /// Used if no refuel station was found along the currently planned route.
        /// </summary>
        private void FindNearestRefuelStationFromList()
        {
            var currentLat = _steeringHandle.Position.Latitude;
            var currentLon = _steeringHandle.Position.Longitude;

            var nearest = _layer.AllRefuelStations.MinBy(r => GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));
            if (nearest != null)
            {
                Console.WriteLine($"Using fallback refuel station from CSV – ID: {nearest.Id} @ ({nearest.Lat}, {nearest.Lon})");
                PlanRouteWithStop(nearest.Lat, nearest.Lon, Route.Stops[_steeringHandle.Route.PassedStops].Edge.To,
                    StopType.Refuel);
            }
            else
            {
                Console.WriteLine("No fallback refuel station found in external list.");
            }
        }


        /// <summary>
        /// Computes squared Euclidean distance between two coordinates (used for nearest neighbor logic).
        /// </summary>
        private double GetSquaredDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = lat1 - lat2;
            double dLon = lon1 - lon2;
            return dLat * dLat + dLon * dLon;
        }

        /// <summary>
        /// Checks whether the truck's current position is on top of a given node (with 1m tolerance).
        /// </summary>
        bool IsOnNode(ISpatialNode node)
        {
            var pos = _steeringHandle.Position;
            var nodePos = node.Position;

            double dx = pos.Latitude - nodePos.Latitude;
            double dy = pos.Longitude - nodePos.Longitude;
            double distance = Math.Sqrt(dx * dx + dy * dy) * 111_000; // degrees to meters

            return distance < 1.0; // within 1 meter radius
        }

        private enum StopType
        {
            Rest,
            Refuel
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

        // Indicates whether overtaking is activated 
        public bool OvertakingActivated { get; set; } = false;

        // Indicates whether braking is activated
        public bool BrakingActivated { get; set; } = false;


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