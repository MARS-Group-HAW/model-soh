using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Layers;
using SOHModel.ChristmasMarket.MovementModels;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A concrete implementation of a MarketTraveler that uses the Collision Free Speed Model for its movement.
/// </summary>
public class CollisionFreeSpeedMarketTraveler : MarketTraveler
{
    /// <summary>
    /// An instance of the pedestrian movement model responsible for calculating agent movement.
    /// This is initialized to the CollisionFreeSpeedMovementModel.
    /// </summary>
    private readonly IPedestrianMovementModel _movementModel = new CollisionFreeSpeedMovementModel();

    /// <summary>
    /// Calculates the agent's next position using the Collision Free Speed Model.
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
        
        var perceptionRadius = 20.0;
        var nearbyTravelers = travelerLayer.GetNearest(Position, perceptionRadius)
            .Where(t => t.ID != this.ID);

        return _movementModel.CalculateNextPosition(
            this,
            _currentStallPosition,
            nearbyTravelers,
            marketBoundary,
            dt);
    }
}