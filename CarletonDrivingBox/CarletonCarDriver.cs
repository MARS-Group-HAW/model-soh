using System;
using System.Linq;
using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Car.Common;
using SOHModel.Car.Model;
using SOHModel.Car.Steering;
using SOHModel.Domain.Steering.Common;

namespace SOHCarletonDrivingBox;

/// <summary>
///     Carleton campus car driver — stock <see cref="CarDriver"/> behaviour plus campus spawn/heatmap fixes
///     and optional vehicle-breakdown / temporary road-block handling (scenario 11).
/// </summary>
public sealed class CarletonCarDriver : AbstractAgent, ICarSteeringCapable
{
    private static readonly Random BreakdownRandom = new();

    public CarletonCarDriver(CarletonCarLayer layer, RegisterAgent register, UnregisterAgent unregister, int driveMode,
        double startLat = 0, double startLon = 0, double destLat = 0, double destLon = 0,
        ISpatialEdge startingEdge = null, string osmRoute = "", string trafficCode = "german")
    {
        ID = Guid.NewGuid();
        Layer = layer;
        _environment = layer.Environment;
        _unregister = unregister;
        _destLat = destLat;
        _destLon = destLon;

        Car = CreateCar();
        Car.Environment = _environment;
        TrafficCode = trafficCode;

        var route = CarRouteFinder.Find(_environment, driveMode,
            startLat, startLon, destLat, destLon, startingEdge, osmRoute);
        var node = _environment.NearestNode(Position.CreateGeoPosition(startLon, startLat));
        _environment.Insert(Car, node);

        Car.TryEnterDriver(this, out _steeringHandle);
        _steeringHandle.Route = route;

        register.Invoke(layer, this);
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
        // Cleared/unregistered in layer PreTick — MARS may still invoke Tick this same step.
        if (_isFinished)
            return;

        if (_isBrokenDown)
        {
            _steeringHandle.Stop();
            return;
        }

        if (Layer.BreakdownProbabilityPerTick > 0 && TryBreakDown())
            return;

        if (Layer.BreakdownProbabilityPerTick > 0)
            LookaheadAndRerouteIfBlocked();

        try
        {
            _steeringHandle.Move();
        }
        catch
        {
            // Graph / route inconsistency must not abort the whole simulation.
            _isFinished = true;
            try
            {
                _environment.Remove(Car);
            }
            catch
            {
                // ignore
            }

            try
            {
                _unregister.Invoke(Layer, this);
            }
            catch
            {
                // ignore
            }

            return;
        }

        if (GoalReached)
        {
            _isFinished = true;
            try
            {
                _environment.Remove(Car);
            }
            catch
            {
                // already removed
            }

            try
            {
                _unregister.Invoke(Layer, this);
            }
            catch
            {
                // already unregistered
            }
        }
    }

    /// <summary>
    /// Called when an edge on this driver's route is blocked by another car's breakdown
    /// (SemiTruck <c>NotifyEdgeBlocked</c> / <c>CreateBypass</c> pattern).
    /// </summary>
    public void NotifyEdgeBlocked(ISpatialEdge blockedEdge)
    {
        if (_isBrokenDown || blockedEdge == null || _steeringHandle.Route == null)
            return;

        // Already on the blocked edge: keep traversing. Rerouting mid-edge can leave CurrentEdge=-1.
        if (ReferenceEquals(Car.CurrentEdge, blockedEdge))
            return;

        var upcoming = _steeringHandle.Route.Stops
            .Skip(_steeringHandle.Route.PassedStops)
            .Any(stop => ReferenceEquals(stop.Edge, blockedEdge));

        if (upcoming)
            TryRerouteToDestination(blockedEdge);
    }

    /// <summary>
    /// Invoked by the layer after the blocked edge is restored; removes the disabled car.
    /// Must not throw — clearance runs in layer PreTick and must not kill the sim.
    /// Marks the agent finished before Remove so a same-tick <see cref="Tick"/> is a no-op
    /// (MARS can still call Tick after PreTick unregister).
    /// </summary>
    public void FinishBreakdownClearance()
    {
        if (_isFinished)
            return;

        _isFinished = true;
        _isBrokenDown = false;
        IsBrokenDown = false;
        try
        {
            if (Car != null && _environment != null)
                _environment.Remove(Car);
        }
        catch
        {
            // already removed / edge already gone
        }

        try
        {
            _unregister.Invoke(Layer, this);
        }
        catch
        {
            // already unregistered
        }
    }

    private bool TryBreakDown()
    {
        lock (BreakdownRandom)
        {
            if (BreakdownRandom.NextDouble() >= Layer.BreakdownProbabilityPerTick)
                return false;
        }

        var edge = Car.CurrentEdge;
        if (edge == null && _steeringHandle.Route is { Count: > 0 })
            edge = _steeringHandle.Route.Stops.Skip(_steeringHandle.Route.PassedStops).FirstOrDefault()?.Edge;

        if (edge == null)
            return false;

        // SemiTruck-style impact: only crash on main campus roads. Parking aisles are
        // single-path — blocking them strands the lot and does not create meaningful detours.
        if (CarletonCarLayer.IsParkingLotEdge(edge))
            return false;

        _isBrokenDown = true;
        IsBrokenDown = true;
        _steeringHandle.Stop();
        if (!Layer.RegisterBreakdown(this, edge))
        {
            _isBrokenDown = false;
            IsBrokenDown = false;
            return false;
        }

        return true;
    }

    private void LookaheadAndRerouteIfBlocked()
    {
        if (_steeringHandle.Route == null || _steeringHandle.Route.Count == 0)
            return;

        foreach (var stop in _steeringHandle.Route.Stops.Skip(_steeringHandle.Route.PassedStops))
        {
            if (!Layer.IsEdgeBlocked(stop.Edge))
                continue;

            if (TryRerouteToDestination(stop.Edge))
                return;

            // No alternate path (SemiTruck: accident holds the road) — wait this tick
            // instead of driving through the soft-block.
            _steeringHandle.Stop();
            return;
        }
    }

    private bool TryRerouteToDestination(ISpatialEdge blockedEdge)
    {
        if (_environment == null)
            return false;

        ISpatialNode startNode = null;
        if (Car.CurrentEdge != null && !Layer.IsEdgeBlocked(Car.CurrentEdge))
            startNode = Car.CurrentEdge.To;
        else if (Car.CurrentEdge != null)
            startNode = Car.CurrentEdge.From;

        startNode ??= _environment.NearestNode(Position);

        var goal = _environment.NearestNode(Position.CreateGeoPosition(_destLon, _destLat));
        if (startNode == null || goal == null)
            return false;

        var newRoute = _environment.FindShortestRoute(
            startNode,
            goal,
            edge => edge.Modalities.Contains(SpatialModalityType.CarDriving) &&
                    !Layer.IsEdgeBlocked(edge) &&
                    !ReferenceEquals(edge, blockedEdge));

        if (newRoute == null || newRoute.Count == 0)
            return false;

        _steeringHandle.Route = newRoute;
        return true;
    }

    #region fields

    private readonly CarSteeringHandle _steeringHandle;
    private readonly UnregisterAgent _unregister;
    private readonly ISpatialGraphEnvironment _environment;
    private readonly double _destLat;
    private readonly double _destLon;
    private bool _isBrokenDown;
    private bool _isFinished;

    #endregion

    #region properties

    private CarletonCarLayer Layer { get; }

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

    /// <summary>Parking lot id (P1–P7) set by the scheduler at spawn.</summary>
    [PropertyDescription(Name = "parkingLot")]
    public string ParkingLot { get; set; } = "";

    [PropertyDescription(Name = "isBrokenDown")]
    public bool IsBrokenDown { get; private set; }

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
            var raw = Car.CurrentEdge.Attributes["osmid"];
            if (raw is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                    return item?.ToString() ?? "-1";
                return "-1";
            }
            var osmId = raw.ToString();
            if (osmId.Length > 0 && osmId[0] == '[')
            {
                var inner = osmId.Trim('[', ']');
                var first = inner.Split(',')[0].Trim();
                return string.IsNullOrEmpty(first) ? "-1" : first;
            }
            return osmId;
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
