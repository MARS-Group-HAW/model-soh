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
        private readonly Random _random = new Random();
        private bool _hasAccident = false;
        private DateTime _lastBreakTime;
        private bool _restAreaPlanned = false;
        private DateTime _pausedUntilTime = DateTime.MinValue;
        private DateTime _refuelUntilTime = DateTime.MinValue;
        private Route _originalRoute; // optional, falls du das brauchst
        private Route _postPauseReturnRoute;
        private bool _goingToRestArea = false;
        private bool _pauseCompleted = false;
        private double _FuelTank;
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
            _FuelTank = SemiTruck.FuelTankLevelLiters;
            DefaultAccidentsPerYear = SemiTruck.AccidentsPerYear;
            
            



            //Define SpatialEdge for driveMode 5 as First Outgoing Edge
            ISpatialEdge startingEdge = null;

            var route = SemiTruckRouteFinder.Find(_environment, DriveMode, StartLat, StartLon, DestLat, DestLon,
                startingEdge, "", SemiTruck.Height, SemiTruck.Mass, SemiTruck.Width, SemiTruck.Length,
                SemiTruck.MaxIncline, _layer.RemovedEdges);
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

        private int _lastCheckedRemovedEdgesVersion = -1;
        private int _accidentTicksRemaining = 0;
        private double _originalMaxSpeed = -1;
        // TimeSpan _maxDrivingTimeWithoutBreak = TimeSpan.FromHours(9);
        TimeSpan _maxDrivingTimeWithoutBreak = TimeSpan.FromHours(9);
        private ISpatialNode _restNode;
        private ISpatialNode _refuelNode = null;
        private double _lastRemainingDistanceToGoal = -1;
        private bool _routeChanged = false;
        private bool _refuelPlanned = false;
        private bool _goingToRefuel = false;
        private bool _isRefueling = false;
        

        /// <summary>
        /// Called during each simulation tick to update the SemiTruck's state.
        /// </summary>
        public void Tick()
        {
            if (_steeringHandle == null)
                return;

            // Truck befindet sich in Pause
            if (HandleRestPause())
                return;
            
            if (HandleRefuelPause())
                return;

            // Unfallbehandlung (laufend)
            if (HandleOngoingAccident())
                return;

            // Unfallchance prüfen
            if (HandleAccidentChance())
                return;

            UpdateFuelConsumption();
            CheckIfRefuelIsRequired();

            // Umleitungen prüfen (wenn Straßen blockiert sind)
            if (!_layer.notifyTrucks)
            {
                if (!LookaheadAndBypassIfNeeded())
                    return;
            }

            // Regeln wie Überholverbot oder Steigung anwenden
            if (_steeringHandle.Route.Count > 0)
            {
                var currentEdge = _steeringHandle.Route[0].Edge;
                AdjustSpeedBasedOnWeather();
                UpdateOvertakingPermission(currentEdge);
                AdjustSpeedBasedOnIncline(currentEdge);
            }

            // Prüfen ob Pause notwendig ist
            if ((_layer._simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                Route.RemainingRouteDistanceToGoal > 100_000)
            {
                CheckAndMoveToRestArea();
                _routeChanged = true;
            }

            // Bewegung auf aktueller Route
            _steeringHandle.Move();

            // Ziel erreicht
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
                List<ISpatialLane> lanes = edgeStop.Edge.Lanes?.ToList();
                var desiredLane = lanes?.FirstOrDefault();
                int desiredLaneIndex = desiredLane != null ? lanes.IndexOf(desiredLane) : -1;
                bypassRoute.Add(edgeStop.Edge, desiredLaneIndex);
            }


            _steeringHandle.Route = bypassRoute;
            if (_layer.notifyTrucks) _layer.RegisterTruckForRoute(this, _steeringHandle.Route);

            return true;
        }

        double CalculateMaxSpeedOnIncline(double inclinePercent)
        {
            double powerWatt = SemiTruck.Power * 1000.0;
            double massKg = SemiTruck.Mass * 1000.0;
            double g = 9.81;
            const double minSpeedMps = 30.0 / 3.6; // 30 km/h in m/s ≈ 2.78

            if (inclinePercent <= 0.0)
                return MaxSpeed; // no incline → full speed

            double denominator = massKg * g * (inclinePercent / 100.0);
            if (denominator == 0) return SemiTruck.MaxSpeed;

            double vMps = powerWatt / denominator;

            return Math.Max(minSpeedMps, Math.Min(vMps, MaxSpeed));
        }

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

        private bool HandleAccidentChance()
        {
            // Unfallwahrscheinlichkeit berechnen
            double scaledAccidentsPerYear = SemiTruck.AccidentsPerYear * (_layer.amountOfTrucks / 650000.0);
            double secondsPerYear = 365.0 * 24 * 60 * 60;
            double ticksPerYear = secondsPerYear / _layer._tickDuration.TotalSeconds;
            double accidentChancePerTick = scaledAccidentsPerYear / ticksPerYear;

            if (_random.NextDouble() < accidentChancePerTick)
            {
                // Durchschnittliche Reaktionszeit: ADAC 41 Minuten
                TimeSpan accidentDuration = TimeSpan.FromMinutes(41);

                if (_steeringHandle.Route.Count > 0)
                {
                    var currentEdge = _steeringHandle.Route[0].Edge;
                    if (currentEdge.Attributes.TryGetValue("shoulder", out var shoulderValue))
                    {
                        var shoulderStr = shoulderValue?.ToString()?.ToLower();
                        if (shoulderStr == "yes" || shoulderStr == "both" || shoulderStr == "left" ||
                            shoulderStr == "right")
                        {
                            accidentDuration = TimeSpan.FromMinutes(2); // Fahrzeug fährt auf Standstreifen
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

        private void AdjustSpeedBasedOnIncline(ISpatialEdge currentEdge)
        {
            if (currentEdge.Attributes.TryGetValue("incline", out var inclineValue))
            {
                double incline = ParseIncline(inclineValue?.ToString());
                double adjustedSpeed = CalculateMaxSpeedOnIncline(incline);

                // Backup original speed once
                if (_originalMaxSpeed < 0)
                    _originalMaxSpeed = SemiTruck.MaxSpeed;

                // Begrenze, wenn Reduktion notwendig
                if (adjustedSpeed < _originalMaxSpeed)
                {
                    MaxSpeed = adjustedSpeed;
                }
                else
                {
                    // Wiederherstellen, wenn keine starke Steigung mehr
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

        private bool HandleOngoingAccident()
        {
            if (!_hasAccident)
                return false;

            if (_accidentTicksRemaining > 0)
            {
                _accidentTicksRemaining--;
                return true; // Ticking, aber nichts weiter tun
            }

            // Unfallzeit ist abgelaufen → Truck entfernen
            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

            _environment.Remove(SemiTruck);
            _unregister.Invoke(_layer, this);

            return true; // Unfall abgeschlossen
        }

        private bool LookaheadAndBypassIfNeeded()
        {
            if (_steeringHandle.Route.Count == 0)
                return true; // Keine Route → kein Lookahead nötig

            double distanceAhead = 0;

            for (int i = 0; i < _steeringHandle.Route.Count; i++)
            {
                var edge = _steeringHandle.Route[i].Edge;
                distanceAhead += edge.Length;

                if (_layer.RemovedEdges.Contains(edge))
                {
                    // Versuche Umleitung
                    _routeChanged = true;
                    return CreateBypass(edge); // false → keine Alternative gefunden → Tick abbrechen
                }

                if (distanceAhead >= 5000)
                    break;
            }

            return true; // Kein Problem → fortsetzen
        }

        private void UpdateOvertakingPermission(ISpatialEdge currentEdge)
        {
            if (currentEdge.Attributes.TryGetValue("overtaking", out var overtakingValue))
            {
                OvertakingActivated = overtakingValue?.ToString()?.ToLower() == "yes";
            }
        }

        private void CheckAndMoveToRestArea()
        {
            if (_restAreaPlanned || _goingToRestArea || _pausedUntilTime > _layer._simulationTime)
                return;

            if ((_layer._simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                Route.RemainingRouteDistanceToGoal > 100_000)
            {
                double accumulatedDistance = 0;

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
                                    Console.WriteLine(
                                        $"Rest Area oder Services in Reichweite gefunden – Kante ID: {connectedEdge.GetId()}, Tag: {tagStr}");
                
                                    var restCoord = connectedEdge.To.Position;
                                    PlanRouteWithStop(restCoord.Latitude, restCoord.Longitude, node, StopType.Rest);
                                    return;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("Keine Rest Area innerhalb der nächsten 100 km gefunden. Suche in CSV...");
                FindNearestRestAreaFromList();
            }
        }

        private bool HandleRestPause()
        {
            // Truck befindet sich in Pause
            if (_pausedUntilTime > _layer._simulationTime)
                return true; // Tick soll abbrechen

            // Pause ist abgeschlossen → zurück zur ursprünglichen Route
            if (_pauseCompleted && _pausedUntilTime <= _layer._simulationTime)
            {
                Console.WriteLine("Rückkehr zur ursprünglichen Route");
                _restAreaPlanned = false;
                _pauseCompleted = false;
                _restNode = null;
                Console.WriteLine("Weiterfahrt auf ursprünglicher Route nach Pause.");
            }

            // Truck erreicht gerade den Rest Node → Pause starten
            if (_goingToRestArea &&
                _steeringHandle.Position != null &&
                _restNode != null &&
                IsOnNode(_restNode))
            {
                Console.WriteLine("Rest Area erreicht. Starte Pause.");
                _pausedUntilTime = _layer._simulationTime + TimeSpan.FromHours(4);
                _lastBreakTime = _layer._simulationTime;
                _goingToRestArea = false;
                _pauseCompleted = true;
                return true; // Tick soll abbrechen
            }

            return false; // Weiter im Tick
        }


        private void PlanRouteWithStop(double targetLat, double targetLon, ISpatialNode insertFromNode,
            StopType stopType)
        {
            // Flags setzen
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

            if (_layer.notifyTrucks)
                _layer.UnregisterTruckFromRoute(this, _steeringHandle.Route);

            // Aktuelle Route sichern
            _originalRoute = new Route();
            foreach (var stop in _steeringHandle.Route)
                _originalRoute.Add(stop.Edge, stop.DesiredLane);

            // 1. Finde Split-Index
            var insertIndex = _steeringHandle.Route.Stops
                .Skip(_steeringHandle.Route.PassedStops)
                .ToList()
                .FindIndex(stop => stop.Edge.From == insertFromNode);

            if (insertIndex == -1)
            {
                Console.WriteLine("Kein passender Splitpunkt in der Route gefunden.");
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

            // 2. Route zum Zwischenziel
            var toStopRoute = SemiTruckRouteFinder.Find(
                _environment, DriveMode,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                targetLat, targetLon,
                null, "", SemiTruck.Height, SemiTruck.Mass,
                SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline,
                _layer.RemovedEdges
            );

            if (stopType == StopType.Rest)
                _restNode = toStopRoute.Stops[^1].Edge.To;
            
            if (stopType == StopType.Refuel)
                _refuelNode = toStopRoute.Stops[^1].Edge.To;

            // 3. Route zurück
            var backRoute = SemiTruckRouteFinder.Find(
                _environment, DriveMode,
                targetLat, targetLon,
                splitNode.Position.Latitude, splitNode.Position.Longitude,
                null, "", SemiTruck.Height, SemiTruck.Mass,
                SemiTruck.Width, SemiTruck.Length, SemiTruck.MaxIncline,
                _layer.RemovedEdges
            );

            if (toStopRoute == null || toStopRoute.Count == 0 || backRoute == null || backRoute.Count == 0)
            {
                Console.WriteLine("Route zum Zwischenziel oder zurück konnte nicht berechnet werden.");
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

            // 4. Neue Route zusammensetzen
            var newRoute = new Route();

            for (int i = 0; i < _steeringHandle.Route.PassedStops + insertIndex; i++)
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

            Console.WriteLine(
                $"Truck fährt zum Zwischenziel ({stopType}) bei ({targetLat}, {targetLon}) und kehrt zurück.");
        }

        private void UpdateFuelConsumption()
        {
            double currentRemainingDistance = _steeringHandle.Route.RemainingRouteDistanceToGoal;
            double distanceDrivenKm = 0.0;

            if (_routeChanged || _lastRemainingDistanceToGoal < 0)
            {
                _routeChanged = false;
            }
            else
            {
                distanceDrivenKm = (_lastRemainingDistanceToGoal - currentRemainingDistance) / 1000.0;
                if (distanceDrivenKm < 0) distanceDrivenKm = 0;

                double fuelUsed = (SemiTruck.FuelConsumptionPer100Km / 100.0) * distanceDrivenKm;
                _FuelTank -= fuelUsed;

                if (_FuelTank <= 0)
                {
                    _FuelTank = 0;
                    // Console.WriteLine($"[Truck {SemiTruck.ID}] Tank leer!");
                }
            }

            _lastRemainingDistanceToGoal = currentRemainingDistance;
        }

        private void CheckIfRefuelIsRequired()
        {
            if (_goingToRefuel || _refuelPlanned) return;

            double availableRangeKm = (_FuelTank / SemiTruck.FuelConsumptionPer100Km) * 100;

            if (availableRangeKm < 100)
            {
                
                if (Route.RemainingRouteDistanceToGoal > 100_000)
                {
                    double accumulatedDistance = 0;
                    Console.WriteLine(
                        $"[Truck {SemiTruck.ID}] Tank reicht nur noch für {availableRangeKm:F1} km – Tankstelle wird gesucht...");
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
                                    if (tagStr == "services" || tagStr == "fuel")
                                    {
                                        var serviceCoord = connectedEdge.To.Position;
                                        var insertFromNode = connectedEdge.From;
                    
                                        PlanRouteWithStop(serviceCoord.Latitude, serviceCoord.Longitude, insertFromNode,
                                            StopType.Refuel);
                                        _refuelPlanned = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    Console.WriteLine(
                        $"[Truck {SemiTruck.ID}] Keine Tankstelle auf aktueller Route gefunden – externe Suche.");
                    FindNearestGasStationsFromList();
                    _refuelPlanned = true;
                }
            }
        }
        
        private bool HandleRefuelPause()
        {
            
            // Truck tankt gerade
            if (_isRefueling && _refuelUntilTime > _layer._simulationTime)
                return true; // Tick abbrechen

            // Tankvorgang abgeschlossen
            if (_isRefueling && _refuelUntilTime <= _layer._simulationTime)
            {
                Console.WriteLine($"[Truck {SemiTruck.ID}] Tanken abgeschlossen – Weiterfahrt.");
                _FuelTank = SemiTruck.FuelTankLevelLiters; // Tank auffüllen
                _isRefueling = false;
                _refuelPlanned = false;
                _refuelNode = null;
                
            }
            
            // Truck erreicht Tankstelle
            if (_goingToRefuel &&
                _steeringHandle.Position != null &&
                _refuelNode != null &&
                IsOnNode(_refuelNode))
            {
                Console.WriteLine($"[Truck {SemiTruck.ID}] Tankstelle erreicht. Starte Tankpause (5 Min).");
                _refuelUntilTime = _layer._simulationTime + TimeSpan.FromMinutes(5);
                _isRefueling = true;
                _goingToRefuel = false;
                return true; // Tick abbrechen
            }

            return false;
        }
        
        private void AdjustSpeedBasedOnWeather()
        {
            if (WeatherLayer == null || Position == null)
                return;

            var point = new NetTopologySuite.Geometries.Point(Longitude, Latitude);
            var affectedZone = WeatherLayer.AllZones
                .FirstOrDefault(z => z.Area.Contains(point) && z.SpeedFactor < 1.0);

            if (_originalMaxSpeed < 0)
                _originalMaxSpeed = SemiTruck.MaxSpeed;

            // Restore original accident rate if previously changed
            SemiTruck.AccidentsPerYear = DefaultAccidentsPerYear;

            if (affectedZone != null)
            {
                MaxSpeed = _originalMaxSpeed * affectedZone.SpeedFactor;

                // Beispielprüfung für Schnee – je nach deinem Datenmodell anpassen!
                if (affectedZone.Type?.ToLower().Contains("schnee") == true ||
                    affectedZone.SpeedFactor <= 0.6) // fallback falls Type nicht verfügbar
                {
                    SemiTruck.AccidentsPerYear *= 2.06;
                    // Console.WriteLine($"[Truck {SemiTruck.ID}] Schneezone erkannt – Unfallrate x2.06");
                }

                // Console.WriteLine($"[Truck {SemiTruck.ID}] Wetterzone aktiv ({affectedZone.Type}) – SpeedFactor: {affectedZone.SpeedFactor}, neue MaxSpeed: {MaxSpeed:F1} km/h");
            }
            else
            {
                MaxSpeed = _originalMaxSpeed;
                // Console.WriteLine($"[Truck {SemiTruck.ID}] Kein Wettereffekt – MaxSpeed ist: {MaxSpeed:F1} km/h");
            }
        }


        private void FindNearestRestAreaFromList()
        {
            var currentLat = _steeringHandle.Position.Latitude;
            var currentLon = _steeringHandle.Position.Longitude;

            var nearest = _layer.AllRestAreas.MinBy(r => GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));
            if (nearest != null)
            {
                Console.WriteLine(
                    $"Verwende Rest Area aus CSV – ID: {nearest.Id}, Koordinaten: ({nearest.Lat}, {nearest.Lon})");
                PlanRouteWithStop(nearest.Lat, nearest.Lon, Route.Stops[_steeringHandle.Route.PassedStops].Edge.To,
                    StopType.Rest);
            }
            else
            {
                Console.WriteLine("Keine passende Rest Area in CSV gefunden.");
            }
        }
        
        private void FindNearestGasStationsFromList()
        {
            var currentLat = _steeringHandle.Position.Latitude;
            var currentLon = _steeringHandle.Position.Longitude;

            var nearest = _layer.AllGasStations.MinBy(r => GetSquaredDistance(currentLat, currentLon, r.Lat, r.Lon));
            if (nearest != null)
            {
                Console.WriteLine(
                    $"Verwende Gas Station aus CSV – ID: {nearest.Id}, Koordinaten: ({nearest.Lat}, {nearest.Lon})");
                PlanRouteWithStop(nearest.Lat, nearest.Lon, Route.Stops[_steeringHandle.Route.PassedStops].Edge.To,
                    StopType.Refuel);
            }
            else
            {
                Console.WriteLine("Keine passende Rest Area in CSV gefunden.");
            }
        }


        private double GetSquaredDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = lat1 - lat2;
            double dLon = lon1 - lon2;
            return dLat * dLat + dLon * dLon;
        }

        bool IsOnNode(ISpatialNode node)
        {
            var pos = _steeringHandle.Position;
            var nodePos = node.Position;

            double dx = pos.Latitude - nodePos.Latitude;
            double dy = pos.Longitude - nodePos.Longitude;
            double distance = Math.Sqrt(dx * dx + dy * dy) * 111_000; // in Metern

            return distance < 1.0; // oder 0.5, je nach Genauigkeit
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