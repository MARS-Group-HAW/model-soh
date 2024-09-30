using Mars.Interfaces.Environments;
using Mars.Numerics;
using SOHModel.Multimodal.Model;

namespace SOHModel.Demonstration;

public class Police : MultiCapableAgent<DemonstrationLayer>
{
    /// <summary>
    ///     The RadicalDemonstrator agent that is currently being chased by the Police agent
    /// </summary>
    private RadicalDemonstrator? _myRadicalDemonstrator;

    /// <summary>
    ///     Police agent stops moving if MultimodalRoute is updated during each tick.
    ///     Therefore, we need to add this stupid counter...
    /// </summary>
    private int _routeUpdateCounter = 60;
    private DemonstrationLayer? _demonstrationLayer;
    
    /// <summary>
    ///     Count the ticks spent on chasing a deviant demonstrator.
    /// </summary>
    private int _currentChasingCounter;
    
    /// <summary>
    ///     The position of the roadblock that is guarded by the Police agent
    /// </summary>
    public Position? Source { get; set; }

    /// <summary>
    ///     Currently not used explicitly
    /// </summary>
    public Position? Target { get; set; }

    /// <summary>
    ///     Currently not used
    /// </summary>
    public int SquadSize { get; set; }

    /// <summary>
    ///     Number of arrests the police agent made.
    /// </summary>
    public int ArrestsCounter { get; set; }

    public int MaxAllowedDistanceToDemonstrators { get; set; } = 150;

    /// <summary>
    ///     Config Parameter: The distance (in meters) that Police agent can search for
    ///     RadicalDemonstrator agents that are breaking out
    /// </summary>
    public double MaxSearchDistance { get; set; } = 400;

    /// <summary>
    ///     The current state of the Police agent (Stationary, Chasing, Returning)
    /// </summary>
    public PoliceState State { get; set; } = PoliceState.Stationary;

    /// <summary>
    ///     Specify the max amount of ticks a police agent spends on chasing a deviant demonstrator.
    /// </summary>
    public int MaxChasingCounter { get; set; } = 180;

    public override void Init(DemonstrationLayer layer)
    {
        base.Init(layer);

        _demonstrationLayer = layer;
        EnvironmentLayer = _demonstrationLayer.SpatialGraphMediatorLayer;
    }

    public override void Tick()
    {
        // Police is at station and looking for a radical demonstrator to chase
        if (State is PoliceState.Stationary)
        {
            var nearestRadicalDemonstrator = FindNearestRadDemBreakingOut(MaxSearchDistance);
            if (nearestRadicalDemonstrator is not null) StartChasingRadDem(nearestRadicalDemonstrator);
        }

        // Police is chasing a radical demonstrator
        if (State is PoliceState.Chasing)
        {
            _routeUpdateCounter -= 1;
            if (_currentChasingCounter >= MaxChasingCounter)
            {
                State = PoliceState.Returning;
                _currentChasingCounter = 0;
            }

            if (_routeUpdateCounter == 0 || GoalReached)
            {
                MultimodalRoute = MultimodalLayer.Search(this, Position, _myRadicalDemonstrator?.Position,
                    ModalChoice.Walking);
                _routeUpdateCounter = 60;
            }

            _currentChasingCounter += 1;
            base.Move();
        }

        // Police is chasing a radical demonstrator who is no longer breaking out. So start returning to station
        if (State == PoliceState.Chasing && _myRadicalDemonstrator?.State != RadicalDemonstratorStates.BreakingOut)
        {
            if (DistanceToNearestDem() > MaxAllowedDistanceToDemonstrators)
            {
                if (_myRadicalDemonstrator?.Arrest() ?? false)
                {
                    ArrestsCounter += 1;
                }
            }

            _myRadicalDemonstrator = null;
            State = PoliceState.Returning;
            _currentChasingCounter = 0;
            MultimodalRoute = MultimodalLayer.Search(this, Position, Source, ModalChoice.Walking);
        }

        // Police is returning to station
        if (State is PoliceState.Returning)
        {
            base.Move();
            var nearestRadicalDemonstrator = FindNearestRadDemBreakingOut(MaxSearchDistance);
            if (nearestRadicalDemonstrator is not null) StartChasingRadDem(nearestRadicalDemonstrator);

            if (GoalReached)
            {
                State = PoliceState.Stationary;
                SetWalking();
            }
        }
    }

    /// <summary>
    ///     Gets the closest radical demonstrators who is breaking out
    /// </summary>
    private double DistanceToNearestDem()
    {
        if (_myRadicalDemonstrator != null)
        {
            var nearestDemonstrator = _demonstrationLayer?.DemonstratorMap.Values.MinBy(demonstrator =>
                demonstrator.Position != null
                    ? _myRadicalDemonstrator.Position.DistanceInMTo(demonstrator.Position)
                    : double.MaxValue);

            if (nearestDemonstrator != null)
            {
                return _myRadicalDemonstrator.Position.DistanceInMTo(nearestDemonstrator.Position);
            }
        }
        return 0;
    }

    /// <summary>
    ///     Gets the closest radical demonstrators who is breaking out
    /// </summary>
    private RadicalDemonstrator? FindNearestRadDemBreakingOut(double maxSearchDistance)
    {
        var distanceToNearestRadDem = double.MaxValue;
        RadicalDemonstrator? nearestRadDem = null;
        var radicalDemonstrators = _demonstrationLayer?.RadicalDemonstratorMap.Values;
        
        if (radicalDemonstrators != null)
            foreach (var radicalDemonstrator in radicalDemonstrators)
            {
                if (radicalDemonstrator.State == RadicalDemonstratorStates.BreakingOut)
                {
                    var distanceToRadDem =
                        Distance.Euclidean(Position.PositionArray, radicalDemonstrator.Position.PositionArray);
                    if (Position.DistanceInMTo(radicalDemonstrator.Position) <= maxSearchDistance)
                    {
                        if (distanceToRadDem < distanceToNearestRadDem)
                        {
                            distanceToNearestRadDem = distanceToRadDem;
                            nearestRadDem = radicalDemonstrator;
                        }
                    }
                }
            }

        return nearestRadDem;
    }

    /// <summary>
    ///     Generates a route to a radical demonstrator that is breaking out and starts chasing him/her
    /// </summary>
    private void StartChasingRadDem(RadicalDemonstrator radicalDemonstrator)
    {
        MultimodalRoute = MultimodalLayer.Search(this, Position, radicalDemonstrator.Position, ModalChoice.Walking);
        _myRadicalDemonstrator = radicalDemonstrator;
        State = PoliceState.Chasing;
        SetRunning();
    }
}