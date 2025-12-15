using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Analytics;
using SOHModel.ChristmasMarket.Entities;
using SOHModel.ChristmasMarket.Layers;
using SOHModel.Multimodal.Model;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// An abstract base class for an agent that visits the Christmas market.
/// It manages the agent's high-level state (e.g., walking to, on, or leaving the market)
/// and decision-making logic, such as choosing which stall to visit next.
/// The actual movement calculation is delegated to each subclass via Template Method Pattern.
/// </summary>
public abstract class MarketTraveler : Traveler<MarketTravelerLayer>
{
    
    [PropertyDescription(Name = "leaveProbability")]
    public double LeaveProbability { get; set; } = 0.01;
    
    [PropertyDescription(Name = "despawnOnArriveHome")]
    public bool DespawnOnArriveHome { get; set; } = true;
    
    public Position CurrentVelocity { get; set; } = new(0, 0);
    
    private MarketStall _lastStallVisited;
    protected MarketStall _targetStall;
    
    private Position _homePosition;
    protected Position _currentStallPosition;
    private int _ticksToWaitAtStall = 0;
    protected Random _random = new Random();
    
    private enum VisitorState
    {
        WalkingToMarket,
        OnMarket,
        WalkingHome
    }
    
    private VisitorState _state = VisitorState.WalkingToMarket;

    /// <summary>
    /// The main entry point for the agent's logic, called once per simulation tick.
    /// It delegates behavior based on the agent's current state.
    /// </summary>
    public override void Tick()
    {
        switch (_state)
        {
            case VisitorState.WalkingToMarket:
                TickWalkingToMarket();
                break;
            case VisitorState.OnMarket:
                SimulateFreeMovement();
                break;
            case VisitorState.WalkingHome:
                TickWalkingHome();
                break;
        }
    }
    
    /// <summary>
    /// Manages the agent's behavior while on the market. This includes waiting at stalls,
    /// deciding when to leave, choosing a new target stall, and triggering the movement calculation.
    /// </summary>
    protected void SimulateFreeMovement()
    {
        if (_ticksToWaitAtStall > 0)
        {
            _ticksToWaitAtStall--;
            CheckForNearbyStalls();
            return;
        }
        
        // At some point the agent decides that it's time to leave the market.
        if (_random.NextDouble() < LeaveProbability)
        {
            FinishMarketVisit();
            return;
        }

        CheckForNearbyStalls();
        
        if (_targetStall == null || Position.DistanceInMTo(_currentStallPosition) < 0.5)
        {
            if (_targetStall != null)
            {
                OnStallVisit(_targetStall);
                _ticksToWaitAtStall = _random.Next(3, 11); // Wait for 3 to 10 ticks at the stall
                _targetStall = null;
                return;
            }
            
            ChooseNewTargetStall();
            if (_targetStall == null) return;
        }
        
        // The actual movement calculation is done by the subclass.
        Position = CalculateNextMovementStep();
    }
    
    /// <summary>
    /// Abstract method that must be implemented by subclasses.
    /// This method is responsible for calculating the agent's next position based on a specific movement model.
    /// </summary>
    /// <returns>The new calculated position for the agent in the next tick.</returns>
    protected abstract Position CalculateNextMovementStep();

    /// <summary>
    /// Handles the agent's behavior while walking from its starting point to the market.
    /// </summary>
    private void TickWalkingToMarket()
    {
        EnsureHomePosition();

        if (IsInsideMarketArea(Position))
        {
            EnterMarket();
            return;
        }

        if (MultimodalRoute == null)
        {
            MultimodalRoute = FindMultimodalRoute();
        }

        base.Move();

        if (IsInsideMarketArea(Position) || MultimodalRoute?.GoalReached == true)
        {
            Console.WriteLine($"[DEBUG] Agent {ID} reached market area (inside: {IsInsideMarketArea(Position)}");
            EnterMarket();
        }
    }

    /// <summary>
    /// Manages the agent's transition into the market area.
    /// </summary>
    private void EnterMarket()
    {
        if (_state == VisitorState.OnMarket)
        {
            return;
        }
        
        EnsureHomePosition();
        MultimodalRoute = null;

        var layer = MarketLayer.Current;
        if (layer == null)
        {
            Console.WriteLine($"[ERROR] Agent {ID} cannot enter market - MarketLayer not found!");
        }
        else
        {
            layer.EnqueueRegister(this);
            Console.WriteLine($"[DEBUG] Agent {ID} entered market at position ({Position?.X:F6}, {Position?.Y:F6}), home: ({_homePosition?.X:F6}, {_homePosition?.Y:F6})");
        }
        
        ChristmasMarketAnalysics.RecordAgentEntry(ID, MarketLayer.Current.Context.CurrentTick);
        
        _state = VisitorState.OnMarket;
    }
    
    /// <summary>
    /// Initiates the agent's departure from the market. It records the exit time,
    /// finds a route home, and changes the agent's state.
    /// </summary>
    protected void FinishMarketVisit()
    {
        var targetHome = _homePosition ?? StartPosition;
        if (targetHome == null)
        {
            Console.WriteLine($"[ERROR] Agent {ID} cannot finish market visit - no home position!");
            return;
        }
        
        StartPosition = Position;
        GoalPosition = targetHome;
        MultimodalRoute = FindMultimodalRoute();
        
        Console.WriteLine($"[DEBUG] Agent {ID} leaving market, walking home to ({GoalPosition?.X:F6}, {GoalPosition?.Y:F6})");
        ChristmasMarketAnalysics.RecordAgentExit(ID, MarketLayer.Current.Context.CurrentTick);
        
        _state = VisitorState.WalkingHome;
    }

    /// <summary>
    /// Handles the agent's behavior while walking from the market back to its home position.
    /// </summary>
    private void TickWalkingHome()
    {
        if (MultimodalRoute == null)
        {
            EnsureHomePosition();

            if (_homePosition == null)
            {
                Console.WriteLine($"[ERROR] Agent {ID} has no home position while walking home!");
                return;
            }

            StartPosition = Position;
            GoalPosition = _homePosition;
            
            MultimodalRoute = FindMultimodalRoute();
            if (MultimodalRoute == null)
            {
                Console.WriteLine($"[WARNING] Agent {ID} cannot find route home, retrying next tick");
                return;
            }
        }

        base.Move();

        var distToHome = Position?.DistanceInMTo(GoalPosition) ?? double.MaxValue;

        if (MultimodalRoute.GoalReached)
        {
            Console.WriteLine($"[DEBUG] Agent {ID} arrived home at ({Position?.X:F6}, {Position?.Y:F6})");
            ArriveHome();
        }
    }

    /// <summary>
    /// Finalizes the agent's journey. It either despawns the agent or resets its state
    /// to begin the cycle again, based on the `DespawnOnArriveHome` property.
    /// </summary>
    private void ArriveHome()
    {
        if (DespawnOnArriveHome)
        {
            Console.WriteLine($"[DEBUG] Agent {ID} despawning after arriving home");
            (MultimodalLayer as MarketTravelerLayer)?.Unregister(this);
        }
        else
        {
            Console.WriteLine($"[DEBUG] Agent {ID} arrived home but not despawning");
            MultimodalRoute = null;
            _state = VisitorState.WalkingToMarket;
        }
    }
    
    /// <summary>
    /// Caches the agent's initial starting position as its "home" to ensure it can return later.
    /// </summary>
    private void EnsureHomePosition()
    {
        if (_homePosition == null && StartPosition != null)
        {
            _homePosition = StartPosition;
            Console.WriteLine($"[DEBUG] Agent {ID} set home position to ({_homePosition?.X:F6}, {_homePosition?.Y:F6})");
        }
    }

    /// <summary>
    /// A protected helper to check if a position is within the market boundaries.
    /// </summary>
    /// <param name="p">The position to check.</param>
    /// <returns>True if the position is inside the market area.</returns>
    protected bool IsInsideMarketArea(Position p)
    {
        var layer = MarketLayer.Current;
        if (layer == null) return false;
        return layer.IsInsideMarketArea(p);
    }
    
    /// <summary>
    /// A protected helper to get the market's boundary polygon.
    /// </summary>
    /// <returns>A list of coordinates defining the market boundary.</returns>
    protected List<(double lon, double lat)> GetMarketPolygon()
    {
        var layer = MarketLayer.Current;
        return layer?.GetMarketPolygon();
    }
    
    /// <summary>
    /// A utility method for debugging that prints a message when an agent is near any stall,
    /// regardless of whether it is their target.
    /// </summary>
    private void CheckForNearbyStalls()
    {
        const double interactionRadius = 2.0;

        var marketLayer = MarketLayer.Current;
        if (marketLayer == null) return;

        var nearestStall = marketLayer.FindNearestStall(Position);
        if (nearestStall == null) return;

        var distanceToStall = Position.DistanceInMTo(nearestStall.Position);
        var newStallToVisit = (distanceToStall < interactionRadius) ? nearestStall : null;

        if (_lastStallVisited != newStallToVisit)
        {
            _lastStallVisited = newStallToVisit;
        }
    }

    /// <summary>
    /// Called when the agent arrives at a target stall.
    /// Can be overridden by subclasses to implement specific behavior (e.g. buying, drinking).
    /// </summary>
    /// <param name="stall">The stall visited.</param>
    protected virtual void OnStallVisit(MarketStall stall)
    {
        ChristmasMarketAnalysics.RecordStallVisit(stall);
    }

    /// <summary>
    /// Selects a new random market stall for the agent to travel to.
    /// Ensures the new stall is different from the current one if possible.
    /// </summary>
    protected virtual void ChooseNewTargetStall()
    {
        var marketLayer = MarketLayer.Current;
        if (marketLayer == null || marketLayer.Stalls.Count == 0)
        {
            _targetStall = null;
            return;
        }

        var allStalls = marketLayer.Stalls;
        MarketStall newTarget;

        if (allStalls.Count <= 1)
        {
            newTarget = allStalls[0];
        }
        else
        {
            do
            {
                newTarget = allStalls[_random.Next(allStalls.Count)];
            } while (newTarget == _targetStall);
        }

        _targetStall = newTarget;
        _currentStallPosition = _targetStall.Position;
        Console.WriteLine(
            $"[DEBUG] Agent {ID} is now walking to stall: '{_targetStall.StallName}' ({_targetStall.Type}).");
    }
    
    /// <summary>
    /// Defines the available modes of transport for this agent. This agent can only walk.
    /// </summary>
    /// <returns>An enumerable containing the walking modal choice.</returns>
    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        yield return ModalChoice.Walking;
    }

    /// <summary>
    /// Finds a route from the agent's start position to its goal position using only walking.
    /// </summary>
    /// <returns>A calculated multimodal route for the agent.</returns>
    protected override MultimodalRoute FindMultimodalRoute()
    {
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoice.Walking);
    }
}