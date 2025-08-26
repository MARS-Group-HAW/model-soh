using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;

namespace SOHModel.ChristmasMarket;

public class RandomWalkMarketTraveler : MarketTraveler
{
    [PropertyDescription(Name = "leaveProbability")]
    public double LeaveProbability { get; set; } = 0.10;
    
    [PropertyDescription(Name = "walkStepMeters")]
    public double WalkStepMeters { get; set; } = 1.0;

    
    private Random _random = new Random();
    
    private Position _currentTarget;
    private List<(double lon, double lat)> _polygonCache;
    private bool _polygonParsed;


    protected override void SimulateFreeMovement()
    {
        if (_random.NextDouble() < LeaveProbability)
        {
            FinishMarketVisit();
            return;
        }

        EnsurePolygon();
        if (_polygonCache == null || _polygonCache.Count < 3 || Position == null)
        {
            return;
        }
        
        if (_currentTarget == null || Position.DistanceInMTo(_currentTarget) < 0.5)
        {
            _currentTarget = RandomPointInPolygon();
            if (_currentTarget == null) return;
        }

        var dx = _currentTarget.X - Position.X;
        var dy = _currentTarget.Y - Position.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-9)
            return;

        var step = Math.Min(WalkStepMeters, dist);
        var nx = Position.X + dx / dist * step;
        var ny = Position.Y + dy / dist * step;

        var tryStep = step;
        var newX = nx;
        var newY = ny;
        int guard = 0;
        while (!PolygonUtils.IsPointInPolygon(newX, newY, _polygonCache) && tryStep > 1e-3 && guard++ < 12)
        {
            tryStep *= 0.5;
            newX = Position.X + dx / dist * tryStep;
            newY = Position.Y + dy / dist * tryStep;
        }

        if (!PolygonUtils.IsPointInPolygon(newX, newY, _polygonCache))
        {
            _currentTarget = null;
            return;
        }
        Position = new Position(newX, newY);
    }
    
    private void EnsurePolygon()
    {
        if (_polygonParsed) return;
        _polygonCache = PolygonUtils.ParsePolygon(TopLeftCorner, TopRightCorner, BottomRightCorner, BottomLeftCorner);
        _polygonParsed = true;
    }

    
    private Position RandomPointInPolygon()
    {
        if (_polygonCache == null || _polygonCache.Count < 3) return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (x, y) in _polygonCache)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        for (int i = 0; i < 50; i++)
        {
            var rx = minX + _random.NextDouble() * (maxX - minX);
            var ry = minY + _random.NextDouble() * (maxY - minY);
            if (PolygonUtils.IsPointInPolygon(rx, ry, _polygonCache))
                return new Position(rx, ry);
        }

        return Position;
    }
}