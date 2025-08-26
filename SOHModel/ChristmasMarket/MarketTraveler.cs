using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket;
using SOHModel.Domain.Model;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.Multimodal.Model;

public abstract class MarketTraveler : Traveler<MarketTravelerLayer>
{
    private enum VisitorState
    {
        WalkingToMarket,
        OnMarket,
        WalkingHome
    }

    private VisitorState _state = VisitorState.WalkingToMarket;

    [PropertyDescription(Name = "topLeftCorner")]
    public string TopLeftCorner { get; set; }

    [PropertyDescription(Name = "topRightCorner")]
    public string TopRightCorner { get; set; }

    [PropertyDescription(Name = "bottomRightCorner")]
    public string BottomRightCorner { get; set; }

    [PropertyDescription(Name = "bottomLeftCorner")]
    public string BottomLeftCorner { get; set; }
    
    [PropertyDescription(Name = "despawnOnArriveHome")]
    public bool DespawnOnArriveHome { get; set; } = true;


    private List<(double lon, double lat)> _marketPolygon;
    private bool _polygonParsed;
    public MarketLayer MarketLayerRef { get; set; }
    private Position _homePosition;


    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        yield return ModalChoice.Walking;
    }

    protected override MultimodalRoute FindMultimodalRoute()
    {
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoice.Walking);
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
    
    // Phase 2 – Marktbewegung implementieren (innerhalb des Polygons)
    protected abstract void SimulateFreeMovement();

    private void TickWalkingToMarket()
    {
        if (_homePosition == null && StartPosition != null)
        {
            _homePosition = StartPosition;
        }

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

        if (IsInsideMarketArea(Position))
        {
            EnterMarket();
            return;
        }

        if (MultimodalRoute == null || MultimodalRoute.GoalReached)
        {
            EnterMarket();
        }
    }

    private void EnterMarket()
    {
        if (_state == VisitorState.OnMarket) return;
        
        if (_homePosition == null && StartPosition != null)
        {
            _homePosition = StartPosition;
        }
        
        MultimodalRoute = null;
        //MultimodalLayer.UnregisterAgent?.Invoke(MultimodalLayer, this);

        var layer = MarketLayerRef ?? MarketLayer.Current;
        if (layer == null)
        {
            layer = MarketLayer.Current;
        }

        if (layer != null)
        {
            layer.EnqueueRegister(this);
        }
        
        _state = VisitorState.OnMarket;
    }
    
    protected void FinishMarketVisit()
    {
        var targetHome = _homePosition ?? StartPosition;
        if (targetHome == null)
        {
            return;
        }
        
        StartPosition = Position;
        GoalPosition = targetHome;
        
        MultimodalRoute = FindMultimodalRoute();
        
        if (MultimodalRoute != null)
        {
            var dist = Position?.DistanceInMTo(GoalPosition) ?? double.MaxValue;
            if (MultimodalRoute.GoalReached && dist > 10.0)
            {
                MultimodalRoute = null;
            }
        }
        _state = VisitorState.WalkingHome;
    }

    private void TickWalkingHome()
    {
        if (MultimodalRoute == null)
        {
            if (_homePosition == null && StartPosition != null)
            {
                _homePosition = StartPosition;
            }

            if (_homePosition != null)
            {
                StartPosition = Position;
                GoalPosition = _homePosition;
                
                MultimodalRoute = FindMultimodalRoute();
                if (MultimodalRoute == null)
                {
                    return;
                }
            }
            var distInit = Position?.DistanceInMTo(GoalPosition) ?? double.MaxValue;
            if (MultimodalRoute.GoalReached && distInit > 10.0)
            {
                MultimodalRoute = null;
                return;
            }
        }

        base.Move();
        
        var dist = Position?.DistanceInMTo(GoalPosition) ?? double.MaxValue;

        if (MultimodalRoute.GoalReached)
        {
            if (dist > 10.0)
            {
                MultimodalRoute = null;
                return;
            }
        }
        
        ArriveHome();
    }
    
    private void ArriveHome()
    {
        if (DespawnOnArriveHome)
        {
            //MultimodalLayer.UnregisterAgent?.Invoke(MultimodalLayer, this);
            (MultimodalLayer as MarketTravelerLayer)?.Unregister(this);

        }
        else
        {
            MultimodalRoute = null;
        }
    }

    private bool IsInsideMarketArea(Position p)
    {
        ParsePolygon();
        if (_marketPolygon == null) return false;
        return PolygonUtils.IsPointInPolygon(p.X, p.Y, _marketPolygon);
    }

    private void ParsePolygon()
    {
        if (_polygonParsed) return;
        _marketPolygon = PolygonUtils.ParsePolygon(TopLeftCorner, TopRightCorner, BottomRightCorner, BottomLeftCorner);
        _polygonParsed = true;
    }
}