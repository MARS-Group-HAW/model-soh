using Mars.Interfaces.Environments;
using Mars.Numerics;

namespace SOHModel.Demonstration;

public class RadicalDemonstrator : Demonstrator
{
    #region Properties and Fields

    /**
     * The current state of the RadicalDemonstrator agent (Demonstrating, BreakingOut, Returning)
     */
    public RadicalDemonstratorStates State { get; set; } = RadicalDemonstratorStates.Demonstrating;

    /**
     * Counts the number of ticks the RadicalDemonstrator spends in the state BreakingOut
     */
    public int BreakingOutCounter { get; set; }

    /**
     * Counts the number of ticks the agent spent on the current breakout attempt.
     */
    private int _currentBreakoutCounter;

    /**
     * CONFIG PARAMETER
     * Specify the maximum amount of time the agent runs, before they escape.
     */
    public int MaxCurrentBreakoutCounter { get; set; } = 180;

    /**
     * Counts the number of times the RadicalDemonstrator broke out
     */
    public int BrokeOutCounter { get; set; }

    /**
     * Counts the number of times the RadicalDemonstrator was forced by Police to return to demonstration route
     */
    public int ReturningCounter { get; set; }

    /**
     * CONFIG PARAMETER
     * The minimum distance (in meters) that must be between a RadicalDemonstrator and nearest Police for
     * RadicalDemonstrator to break out
     */
    public int DistanceForBreakingOut { get; set; } = new Random().Next(10, 30);

    /**
     * CONFIG PARAMETER
     * The minimum distance (in meters) that must be between a RadicalDemonstrator and nearest Police for
     * RadicalDemonstrator to return to demonstration route
     */
    public int DistanceForReturning { get; set; } = new Random().Next(30, 50);

    /**
     * CONFIG PARAMETER
     * Maximum number of ticks (i.e., seconds) until RadicalDemonstrator breaks out the next time
     * (5 min * 60 sec/min = 300 sec)
     */
    public int MaxTicksUntilNextBreakout { get; set; } = 300;

    /**
     * Current number of ticks (i.e., seconds)  until RadicalDemonstrator breaks out the next time
     */
    public int CurrentTicksUntilNextBreakout { get; set; }

    /**
     * CONFIG PARAMETER
     * Counter that represents the maximum condition/fitness of the RadicalDemonstrator
     * (used to toggle between Walking and Running)
     */
    public int MaxConditionCounter { get; set; } = new Random().Next(30, 50);

    /**
     * Counter that represents the current condition/fitness of the RadicalDemonstrator
     * (used to toggle between Walking and Running)
     */
    public float CurrentConditionCounter { get; set; }

    private List<EdgeStop>? _demoRouteEdgeStops;
    private DemonstrationLayer? _demonstrationLayer;

    #endregion

    #region Initialization

    public override void Init(DemonstrationLayer layer)
    {
        base.Init(layer);
        _demonstrationLayer = layer;
        EnvironmentLayer = layer.SpatialGraphMediatorLayer;
        MultimodalRoute = GetDemonstrationRoute();
        _demoRouteEdgeStops = GetDemoRouteEdgeStops();
        CurrentConditionCounter = MaxConditionCounter;
        CurrentTicksUntilNextBreakout =
            new Random().Next(MaxTicksUntilNextBreakout - 10, MaxTicksUntilNextBreakout + 500);
    }

    #endregion

    #region Tick

    public override void Tick()
    {
        // Move if not arrested and not escaped
        if (State != RadicalDemonstratorStates.Arrested && State != RadicalDemonstratorStates.Escaped) base.Move();

        // Demonstrating and not yet ready to break out. Therefore, decrement the property
        if (State == RadicalDemonstratorStates.Demonstrating && CurrentTicksUntilNextBreakout > 0)
        {
            CurrentTicksUntilNextBreakout -= 1;
        }

        // Demonstrating and searching for opportunity to break out
        if (State == RadicalDemonstratorStates.Demonstrating &&
            Position.DistanceInMTo(EnvironmentLayer.Environment.NearestNode(Position).Position) < 2 &&
            CurrentTicksUntilNextBreakout == 0)
        {
            if (Position.DistanceInMTo(GetNearestPolice()?.Position) > DistanceForBreakingOut)
            {
                BrokeOutCounter += 1;
                SetRunning();
                State = RadicalDemonstratorStates.BreakingOut;
                var goal = EnvironmentLayer.Environment.GetRandomNode().Position;
                MultimodalRoute = MultimodalLayer.Search(this, Position, goal, ModalChoice.Walking);
            }
        }

        // Currently breaking out, so increment breakout counter
        if (State == RadicalDemonstratorStates.BreakingOut)
        {
            BreakingOutCounter += 1;
            if (_currentBreakoutCounter >= MaxCurrentBreakoutCounter)
            {
                State = RadicalDemonstratorStates.Escaped;
                // MultimodalLayer.UnregisterAgent(MultimodalLayer, this);
            }

            if (Math.Abs(PreferredSpeed - WalkingShoes.RunningSpeed) < 0.1) CurrentConditionCounter -= 1;
            if (CurrentConditionCounter <= 0) SetWalking();

            if (Math.Abs(PreferredSpeed - WalkingShoes.WalkingSpeed) < 0.1) CurrentConditionCounter += 0.5f;
            if (CurrentConditionCounter >= MaxConditionCounter) SetRunning();

            _currentBreakoutCounter += 1;
        }

        // Breaking out and getting too close to police. So stop breaking out and start returning to demonstration route
        if (State == RadicalDemonstratorStates.BreakingOut &&
            Position.DistanceInMTo(GetNearestPolice()?.Position) < DistanceForReturning)
        {
            State = RadicalDemonstratorStates.Returning;
            ReturningCounter += 1;
            _currentBreakoutCounter = 0;
            var nearestReturnPosition = GetReturnPosition();
            MultimodalRoute = MultimodalLayer.Search(this, Position, nearestReturnPosition, ModalChoice.Walking);
            SetWalking();
        }

        // Breaking out and goal reached. Find a new goal and continue breaking out
        if (State == RadicalDemonstratorStates.BreakingOut && GoalReached)
        {
            var goal = EnvironmentLayer.Environment.GetRandomNode().Position;
            MultimodalRoute = MultimodalLayer.Search(this, Position, goal, ModalChoice.Walking);
        }

        // Arrived at demonstration route. So start demonstrating again.
        if (State == RadicalDemonstratorStates.Returning && GoalReached)
        {
            State = RadicalDemonstratorStates.Demonstrating;
            CurrentTicksUntilNextBreakout = MaxTicksUntilNextBreakout;
            SetWalking();
            MultimodalRoute = new MultimodalRoute(CreateNewDemoRoute(), ModalChoice.Walking);
        }
    }

    #endregion

    #region Methods

    /**
     * Performs arrest of radical demonstrator
     */
    public bool Arrest()
    {
        if (State is RadicalDemonstratorStates.Arrested or RadicalDemonstratorStates.Escaped) return false;
        State = RadicalDemonstratorStates.Arrested;
        // MultimodalLayer.UnregisterAgent(MultimodalLayer, this);
        return true;
    }

    /**
     * Returns a list ot edge stops that make up the demonstration route
     */
    private List<EdgeStop> GetDemoRouteEdgeStops()
    {
        var demoRouteEdgeStops = new List<EdgeStop>();

        foreach (var routeStop in MultimodalRoute.Stops)
        {
            foreach (var edgeStop in routeStop.Route)
            {
                demoRouteEdgeStops.Add(edgeStop);
            }
        }

        return demoRouteEdgeStops;
    }

    /**
     * Gets a position in the demonstration route to which RadicalDemonstrator returns after being caught by Police
     */
    private Position GetReturnPosition()
    {
        var nearestPosition = new Position();
        var minDistance = double.MaxValue;

        if (_demoRouteEdgeStops is not null)
        {
            foreach (var edgeStop in _demoRouteEdgeStops)
            {
                var currentPosition = edgeStop.Edge.Geometry[0];
                var currentDistance = Position.DistanceInMTo(currentPosition);

                if (currentDistance < minDistance)
                {
                    minDistance = currentDistance;
                    nearestPosition = currentPosition;
                }
            }
        }

        return nearestPosition;
    }

    /**
     * Returns the nearest Police agent
     */
    private Police? GetNearestPolice()
    {
        return _demonstrationLayer?.PoliceMap.Values.MinBy(police =>
            Distance.Euclidean(Position.PositionArray, police.Position.PositionArray));
    }

    /**
    * Create a new demo route for the radical demonstrator after he/she has returned to the demonstration route
    */
    private Route CreateNewDemoRoute()
    {
        var startPosition = GetReturnPosition();
        var newDemoRoute = new Route();
        var routeFlag = false;

        if (_demoRouteEdgeStops is not null)
        {
            foreach (var edgeStop in _demoRouteEdgeStops)
            {
                //if (Position.DistanceInMTo(edgeStop.Edge.Geometry[0]) < 10 || routeFlag)
                if (startPosition.Equals(edgeStop.Edge.Geometry[0]) || routeFlag)
                {
                    newDemoRoute.Add(edgeStop.Edge);
                    routeFlag = true;
                }
            }
        }

        return newDemoRoute;
    }

    #endregion
}