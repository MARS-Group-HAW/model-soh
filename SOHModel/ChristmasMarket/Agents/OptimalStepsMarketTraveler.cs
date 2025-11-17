using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Layers;
using SOHModel.ChristmasMarket.MovementModels;

namespace SOHModel.ChristmasMarket.Agents;

/// <summary>
/// A concrete implementation of a MarketTraveler that uses the Optimal Steps Model for its movement.
/// </summary>
public class OptimalStepsMarketTraveler : MarketTraveler
{
    /// <summary>
    /// An instance of the pedestrian movement model responsible for calculating agent movement.
    /// This is initialized to the OptimalStepsMovementModel.
    /// </summary>
    private readonly IPedestrianMovementModel _movementModel = new OptimalStepsMovementModel();

    /// <summary>
    /// Calculates the agent's next position using the Optimal Steps Model.
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
        
        // This model can have a shorter perception radius than other models
        // since it only needs to consider nearby agents that are on 'Neighbors fields'.
        var perceptionRadius = 2.0;
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