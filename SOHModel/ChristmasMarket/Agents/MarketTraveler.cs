using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket;
using SOHModel.Domain.Model;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.Multimodal.Model;

public abstract class MarketTraveler : Traveler<MarketTravelerLayer>
{
    
    [PropertyDescription(Name = "leaveProbability")]
    public double LeaveProbability { get; set; } = 0.01;
    
    protected Random _random = new Random();
    private MarketStall _lastStallVisited;
    protected MarketStall _targetStall;
    protected Position _currentStallPosition;
    private VisitorState _state = VisitorState.WalkingToMarket;
    public Position CurrentVelocity { get; set; } = new(0, 0);
    
    [PropertyDescription(Name = "despawnOnArriveHome")]
    public bool DespawnOnArriveHome { get; set; } = true;
    
    private Position _homePosition;
    private int _ticksToWaitAtStall = 0;
    
    private enum VisitorState
    {
        WalkingToMarket,
        OnMarket,
        WalkingHome
    }

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
    
    // Die Template-Methode, die die gemeinsame Logik enthält
    protected void SimulateFreeMovement()
    {
        // --- NEU: Wenn wir gerade warten, tun wir nichts anderes ---
        if (_ticksToWaitAtStall > 0)
        {
            _ticksToWaitAtStall--; // Eine Sekunde/Tick warten
            CheckForNearbyStalls(); // Wir wollen trotzdem prüfen, ob wir nahe sind
            return; // Beende die Methode für diesen Tick
        }
        
        // 1. STRATEGISCHE LOGIK (ist jetzt hier zu Hause)
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
                _ticksToWaitAtStall = _random.Next(3, 11); 
                
                // NEU: Den Besuch des Standes für die Analyse aufzeichnen
                SimulationAnalytics.RecordStallVisit(_targetStall);
                
                Console.WriteLine($"[DEBUG] Agent {ID} reached stall '{_targetStall.StallName}'. Waiting for {_ticksToWaitAtStall} ticks.");
                
                _targetStall = null;
                return;
            }
            
            ChooseNewTargetStall();
            if (_targetStall == null) return;
        }
        
        // 2. OPERATIVE BEWEGUNG (Subklassen)
        Position = CalculateNextMovementStep();
    }
    
    // Phase 2 – Marktbewegung innerhalb des Polygons
    protected abstract Position CalculateNextMovementStep();

    private void TickWalkingToMarket()
    {
        EnsureHomePosition();

        if (IsInsideMarketArea(Position))
        {
            Console.WriteLine($"[DEBUG] Agent {ID} already inside market area, entering immediately");
            EnterMarket();
            return;
        }

        if (MultimodalRoute == null)
        {
            MultimodalRoute = FindMultimodalRoute();
            if (MultimodalRoute == null)
            {
                Console.WriteLine($"[WARNING] Agent {ID} could not find route to market from ({StartPosition?.X:F6}, {StartPosition?.Y:F6}) to ({GoalPosition?.X:F6}, {GoalPosition?.Y:F6})");
            }
        }

        base.Move();

        if (IsInsideMarketArea(Position) || MultimodalRoute?.GoalReached == true)
        {
            Console.WriteLine($"[DEBUG] Agent {ID} reached market area (inside: {IsInsideMarketArea(Position)}, goalReached: {MultimodalRoute?.GoalReached})");
            EnterMarket();
        }
    }

    private void EnterMarket()
    {
        if (_state == VisitorState.OnMarket)
        {
            Console.WriteLine($"[DEBUG] Agent {ID} already on market, ignoring EnterMarket call");
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
        
        SimulationAnalytics.RecordAgentEntry(ID, MarketLayer.Current.Context.CurrentTick);
        
        _state = VisitorState.OnMarket;
    }
    
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
        
        if (MultimodalRoute == null)
        {
            Console.WriteLine($"[WARNING] Agent {ID} could not find route home from ({StartPosition?.X:F6}, {StartPosition?.Y:F6}) to ({GoalPosition?.X:F6}, {GoalPosition?.Y:F6})");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Agent {ID} leaving market, walking home to ({GoalPosition?.X:F6}, {GoalPosition?.Y:F6})");
        }
        
        // NEU: Zeitpunkt des Marktverlassens für die Analyse aufzeichnen
        SimulationAnalytics.RecordAgentExit(ID, MarketLayer.Current.Context.CurrentTick);
        
        _state = VisitorState.WalkingHome;
    }

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
    
    private void EnsureHomePosition()
    {
        if (_homePosition == null && StartPosition != null)
        {
            _homePosition = StartPosition;
            Console.WriteLine($"[DEBUG] Agent {ID} set home position to ({_homePosition?.X:F6}, {_homePosition?.Y:F6})");
        }
    }


    protected bool IsInsideMarketArea(Position p)
    {
        var layer = MarketLayer.Current;
        if (layer == null) return false;
        return layer.IsInsideMarketArea(p);
    }
    
    protected List<(double lon, double lat)> GetMarketPolygon()
    {
        var layer = MarketLayer.Current;
        return layer?.GetMarketPolygon();
    }
    
    private void CheckForNearbyStalls()
    {
        const double interactionRadius = 2.0;

        var marketLayer = MarketLayer.Current;
        if (marketLayer == null) return;

        var nearestStall = marketLayer.FindNearestStall(Position);
        if (nearestStall == null) return;

        var distanceToStall = Position.DistanceInMTo(nearestStall.Position);

        if (distanceToStall < interactionRadius)
        {
            if (_lastStallVisited != nearestStall)
            {
                Console.WriteLine(
                    $"[AAAAAAAAAAAAA] Agent {ID} ist in der Nähe von Stand '{nearestStall.StallName}' ({nearestStall.Type}).");
                _lastStallVisited = nearestStall;
            }
        }
        else
        {
            if (_lastStallVisited != null)
            {
                _lastStallVisited = null;
            }
        }
    }

    /// <summary>
    /// Selects a new random market stall for the agent to travel to.
    /// Ensures the new stall is different from the current one if possible.
    /// </summary>
    private void ChooseNewTargetStall()
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
    
    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        yield return ModalChoice.Walking;
    }

    protected override MultimodalRoute FindMultimodalRoute()
    {
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoice.Walking);
    }
}