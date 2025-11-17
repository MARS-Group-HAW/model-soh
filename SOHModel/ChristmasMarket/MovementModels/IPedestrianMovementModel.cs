using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Agents;

namespace SOHModel.ChristmasMarket;

public interface IPedestrianMovementModel
{
    /// <summary>
    /// Berechnet die nächste Position für einen Agenten basierend auf den Regeln des Modells.
    /// </summary>
    /// <param name="traveler">Der Agent, der sich bewegt.</param>
    /// <param name="targetPosition">Die aktuelle Zielposition (z.B. ein Stand).</param>
    /// <param name="nearbyTravelers">Andere Agenten in der Nähe (für Kollisionsvermeidung).</param>
    /// <param name="marketBoundary">Das Polygon, das die Grenzen des Marktes definiert.</param>
    /// <param name="dt">Die Dauer des Simulationsticks in Sekunden.</param>
    /// <returns>Die neue, gültige Position des Agenten.</returns>
    Position CalculateNextPosition(
        MarketTraveler traveler,
        Position targetPosition,
        IEnumerable<MarketTraveler> nearbyTravelers,
        List<(double lon, double lat)> marketBoundary,
        double dt);
}