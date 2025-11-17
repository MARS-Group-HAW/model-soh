using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Layers;
using SOHModel.ChristmasMarket.MovementModels;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A concrete implementation of a MarketTraveler that uses the Social Forces Model for its movement.
/// </summary>
public class SocialForceMarketTraveler : MarketTraveler
{
    /// <summary>
    /// An instance of the pedestrian movement model responsible for calculating agent movement.
    /// This is initialized to the SocialForcesMovementModel.
    /// </summary>
    private readonly IPedestrianMovementModel _movementModel = new SocialForcesMovementModel();

    /// <summary>
    /// Calculates the agent's next position using the Social Forces Model.
    /// </summary>
    /// <returns>The calculated new Position for the agent in the next simulation tick.</returns>
    protected override Position CalculateNextMovementStep()
    {
        var marketBoundary = GetMarketPolygon();
        var dt = MultimodalLayer?.Context?.OneTickTimeSpan?.TotalSeconds ?? 1.0;
        
        var travelerLayer = MultimodalLayer as IMarketTravelerLayer;
        if (travelerLayer == null)
        {
            return Position;
        }
        
        var perceptionRadius = 5.0;
        var nearbyTravelers = travelerLayer.GetNearest(Position, perceptionRadius)
            .OfType<MarketTraveler>()
            .Where(t => t.ID != this.ID);

        return _movementModel.CalculateNextPosition(
            this,
            _currentStallPosition,
            nearbyTravelers,
            marketBoundary,
            dt);
    }
}