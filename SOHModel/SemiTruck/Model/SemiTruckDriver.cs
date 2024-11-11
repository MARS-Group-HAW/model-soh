using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.SemiTruck.Steering;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Common;

namespace SOHModel.SemiTruck.Model;

/// <summary>
///     Implementation of a driver agent that is responsible for managing a single SemiTruck and
///     dynamically directing its movement to a specified destination.
/// </summary>
public sealed class SemiTruckDriver : AbstractAgent, ISemiTruckSteeringCapable
{
    private static int _stableId;

    private SemiTruckSteeringHandle _steeringHandle;
    private readonly UnregisterAgent _unregister;
    private readonly ISpatialGraphEnvironment _environment;

    private long _departureTick;

    public SemiTruckDriver(SemiTruckLayer layer, RegisterAgent register, UnregisterAgent unregister, 
        double startLat, double startLon, 
        double destLat, double destLon, 
        string truckType = "StandardTruck", 
        ISpatialEdge startingEdge = null, string trafficCode = "german")
    {
        Layer = layer;
        _environment = layer.GraphEnvironment;
        _unregister = unregister;

        // Initialize the truck with the specified type
        SemiTruck = InitializeTruck(truckType);
        SemiTruck.Environment = _environment;
        TrafficCode = trafficCode;

        // Calculate dynamic route based on start and destination coordinates
        var route = SemiTruckRouteFinder.Find(_environment, startLat, startLon, destLat, destLon, startingEdge);
        var node = route.First().Edge.From;
        _environment.Insert(SemiTruck, node);

        SemiTruck.TryEnterDriver(this, out _steeringHandle);
        _steeringHandle.Route = route;

        register.Invoke(layer, this);
    }

    public void Notify(PassengerMessage passengerMessage)
    {
        // Implement your logic here; for now, similar to CarDriver, we can unregister the agent when the goal is reached.
        if (passengerMessage == PassengerMessage.GoalReached)
            _unregister.Invoke(Layer, this);
    }
    
    /// <summary>
    ///     Initializes the truck and sets properties specific to SemiTruck.
    /// </summary>
    /// <param name="truckType">The type of truck to initialize (e.g., "StandardTruck", "TankerTruck")</param>
    private SemiTruck InitializeTruck(string truckType)
    {
        var truck = Layer.EntityManager.Create<SemiTruck>("type", truckType);
        truck.Layer = Layer;
        return truck;
    }

    public override void Tick()
    {
        _steeringHandle.Move();

        if (GoalReached)
        {
            _environment.Remove(SemiTruck);
            _unregister.Invoke(Layer, this);
        }
    }

    #region properties

    public Position Position
    {
        get => SemiTruck.Position;
        set => SemiTruck.Position = value;
    }

    public Route Route => _steeringHandle.Route;

    public double Latitude => Position.Latitude;

    public double Longitude => Position.Longitude;

    [PropertyDescription(Name = "velocity")]
    public double Velocity
    {
        get => SemiTruck.Velocity;
        set => SemiTruck.Velocity = value;
    }

    [PropertyDescription(Name = "maxSpeed")]
    public double MaxSpeed
    {
        get => SemiTruck.MaxSpeed;
        set => SemiTruck.MaxSpeed = value;
    }

    public double SpeedLimit => _steeringHandle.SpeedLimit;

    public double RemainingDistanceOnEdge => _steeringHandle.RemainingDistanceOnEdge;

    public double PositionOnEdge => SemiTruck.PositionOnCurrentEdge;

    [PropertyDescription(Name = "stableId")]
    public string StableId { get; set; } = _stableId++.ToString();

    public bool GoalReached => _steeringHandle.GoalReached;

    public SemiTruck SemiTruck { get; set; }

    public bool OvertakingActivated { get; set; }
    public bool BrakingActivated { get; set; }

    public bool CurrentlyTruckDriving => true;

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
    ///     Gets or sets the intersection behaviour model identified by code when no traffic signals are available.
    ///     "german" = right before left rule
    ///     "southAfrica" = first in first out (FIFO) rule
    /// </summary>
    [PropertyDescription(Name = "trafficCode", Ignore = true)]
    public string TrafficCode
    {
        get => SemiTruck.TrafficCode;
        set => SemiTruck.TrafficCode = value;
    }

    private SemiTruckLayer Layer { get; }

    #endregion
}
