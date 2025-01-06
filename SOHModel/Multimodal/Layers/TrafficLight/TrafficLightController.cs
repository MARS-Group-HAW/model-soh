using Mars.Common;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Multimodal.Layers.TrafficLight;

public class TrafficLightController : IPositionable, IEntity, INodeGuard
{
    public const int GreenDuration = 20;
    public const int YellowDuration = 3;
    
    

    // we saved these for that we know for every controller object at which traffic light we currently are
    public double lat;
    public double lon;

    private readonly ISpatialNode _node;
    private readonly TrafficLightLayer _trafficLightLayer;
    private Dictionary<Tuple<ISpatialEdge, ISpatialEdge>, TrafficLight> _roadLightMappings;
    private List<int> phases;
    private int tickCounter;

    public TrafficLightController(ILayer layer, ISpatialGraphEnvironment environment, double lat, double lon, TrafficLightData tlData)
    {
        _trafficLightLayer = (TrafficLightLayer)layer;
        Position = Position.CreateGeoPosition(lon, lat);
        this.lat = lat;
        this.lon = lon;
        this.phases = tlData.GetPhasesForCoordinate(lat, lon);
        this.tickCounter = 0;
        
        
        _node = environment.NearestNode(Position);
        var distance = _node.Position.DistanceInKmTo(Position) * 1000;
        if (distance > 10)
            _trafficLightLayer.Logger.LogWarning(
                "Traffic light that is supposed" +
                " to be at lat: " + lat +
                " lon: " + lon + " is " + distance +
                "m away from its designated " +
                "position");

        _node.NodeGuard = this;
    }

    public int CycleLength { get; set; }
    public long CurrentTick { get; set; }

    public Guid ID { get; set; } = Guid.NewGuid();

    public TrafficLightPhase GetTrafficLightPhase(ISpatialEdge from, ISpatialEdge to)
    {
        return _roadLightMappings[new Tuple<ISpatialEdge, ISpatialEdge>(from, to)].TrafficLightPhase;
    }

    public Position Position { get; set; }


    public bool AccessEdge(long tick, ISpatialEdge from, ISpatialEdge to)
    {
        var currentPhase = _roadLightMappings[new Tuple<ISpatialEdge, ISpatialEdge>(from, to)].TrafficLightPhase;
        if (currentPhase != TrafficLightPhase.Red)
            return true;

        return false;
    }

    public void UpdateLightPhase()
    {
        
        foreach (var tuple in _roadLightMappings)
            if (phases[tickCounter] == 1)
                tuple.Value.TrafficLightPhase = TrafficLightPhase.Red;
            else if (phases[tickCounter] == 2)
                tuple.Value.TrafficLightPhase = TrafficLightPhase.Yellow;
            else if (phases[tickCounter] == 3)
                tuple.Value.TrafficLightPhase = TrafficLightPhase.Green;
        tickCounter++;
        
        // CurrentTick++;
        // if (CycleLength == 0 || _trafficLightLayer.Context.CurrentTick % CycleLength == 1) CurrentTick = 0;
        //
        // foreach (var tuple in _roadLightMappings)
        //     if (tuple.Value.StartRedTick == CurrentTick)
        //         tuple.Value.TrafficLightPhase = TrafficLightPhase.Red;
        //     else if (tuple.Value.StartYellowTick == CurrentTick)
        //         tuple.Value.TrafficLightPhase = TrafficLightPhase.Yellow;
        //     else if (tuple.Value.StartGreenTick == CurrentTick)
        //         tuple.Value.TrafficLightPhase = TrafficLightPhase.Green;
    }

    // todo insert our own schedule based on our traffic light rt-data
    public void GenerateTrafficSchedules()
    {
        var greenStartTick = 0; 

        _roadLightMappings = new Dictionary<Tuple<ISpatialEdge, ISpatialEdge>, SOHModel.Multimodal.Layers.TrafficLight.TrafficLight>();

        if (_node.IncomingEdges.Count == 0)
        {
            _trafficLightLayer.Logger.LogWarning("Traffic light controller at position (" + Position[0] + "/" +
                                                 Position[1] + ") on node with position (" +
                                                 _node.Position.Latitude +
                                                 "/" + _node.Position.Longitude + ") has no incoming edges");
            CycleLength = 100;
        }

        foreach (var incomingEdge in _node.IncomingEdges.Values)
        {
            double incomingBearing;
            //calculate incoming bearing either from the geometry stored inside an edge
            //or from start and end point of the edge if there is no geometry information
            if (incomingEdge.Geometry != null && incomingEdge.Geometry.Length > 0)
                incomingBearing = incomingEdge.Geometry.Last().GetBearing(incomingEdge.To.Position);
            else
                incomingBearing = incomingEdge.From.Position.GetBearing(incomingEdge.To.Position);


            foreach (var adjacentEdge in _node.OutgoingEdges.Values)
            {
                double outgoingBearing;
                //calculate outgoing bearing either from the geometry stored inside an edge
                //or from start and end point of the edge if there is no geometry information
                if (adjacentEdge.Geometry != null && adjacentEdge.Geometry.Length > 0)
                    outgoingBearing = adjacentEdge.Geometry.Last().GetBearing(adjacentEdge.To.Position);
                else
                    outgoingBearing = adjacentEdge.From.Position.GetBearing(adjacentEdge.To.Position);


                var direction = PositionHelper.GetDirectionType(incomingBearing, outgoingBearing);
                if (direction != DirectionType.Down)
                {
                    var trafficLight = new SOHModel.Multimodal.Layers.TrafficLight.TrafficLight(TrafficLightPhase.Red, greenStartTick,
                        greenStartTick + GreenDuration,
                        greenStartTick + GreenDuration + YellowDuration);
                    _roadLightMappings.Add(new Tuple<ISpatialEdge, ISpatialEdge>(incomingEdge, adjacentEdge),
                        trafficLight);
                }
                else
                {
                    var trafficLight = new SOHModel.Multimodal.Layers.TrafficLight.TrafficLight(TrafficLightPhase.None, 1000, 1000, 1000);
                    _roadLightMappings.Add(new Tuple<ISpatialEdge, ISpatialEdge>(incomingEdge, adjacentEdge),
                        trafficLight);
                }
            }

            greenStartTick += GreenDuration + YellowDuration;
        }

        //prolong light phase
        if (_roadLightMappings.Count < 2)
            greenStartTick += greenStartTick;

        CycleLength = greenStartTick;
        CurrentTick = 0;
    }
}