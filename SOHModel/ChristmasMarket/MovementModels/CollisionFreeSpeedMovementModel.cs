using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.ChristmasMarket.Utils;

namespace SOHModel.ChristmasMarket.MovementModels;

/// <summary>
/// Implements a pedestrian movement model based on the "Collision Free Speed Model".
/// This model calculates an agent's movement by separating the decision-making process into two parts:
/// 1. Optimal Velocity: Adjusting speed to avoid collisions with agents directly in front.
/// 2. Repulsion Force: Adjusting direction to avoid collisions with all nearby agents.
/// The parameters are based on values suggested in the academic paper.
/// </summary>
public class CollisionFreeSpeedMovementModel : IPedestrianMovementModel
{
    // -- Model Parameters (from Figure 2 in the paper) --

    // Optimal Velocity (OV) function parameters
    private const double PedestrianDiameterL = 0.3; // (l) Pedestrian size
    private const double TimeGapT = 1.0; // (T) Time gap in following situations

    // Repulsion function parameters
    private const double RepulsionA = 5.0; // (a) Repulsion strength coefficient
    private const double RepulsionD = 0.1; // (D) Repulsion distance/range

    /// <summary>
    /// Calculates the next position for a market traveler using the Collision-Free Speed Model.
    /// </summary>
    /// <param name="traveler">The agent for which to calculate the next movement step.</param>
    /// <param name="targetPosition">The agent's current destination (a market stall).</param>
    /// <param name="nearbyTravelers">An enumerable of other agents in the area.</param>
    /// <param name="marketBoundary">A list of coordinates defining the valid movement area.</param>
    /// <param name="dt">The simulation time step (delta time) in seconds.</param>
    /// <returns>The new, calculated Position for the agent in the next tick.</returns>
    public Position CalculateNextPosition(
        MarketTraveler traveler,
        Position targetPosition,
        IEnumerable<MarketTraveler> nearbyTravelers,
        List<(double lon, double lat)> marketBoundary,
        double dt)
    {
        var currentPosition = traveler.Position;
        if (currentPosition == null || targetPosition == null)
        {
            return traveler.Position;
        }

        // --- 1. Determine Speed V(s_i) based on pedestrians IN FRONT ---
        var currentVelocityVec = new Vector2D(traveler.CurrentVelocity.X, traveler.CurrentVelocity.Y);
        var currentDirection = currentVelocityVec.Magnitude() > 1e-6 ? currentVelocityVec.Normalized() : Vector2D.Zero;

        double minSpacingInFront = double.MaxValue;

        var others = nearbyTravelers.ToList();
        foreach (var other in others)
        {
            var vecToOther = ToLocalVector(traveler.Position, other.Position);
            var distance = vecToOther.Magnitude();

            // Check if other is in front ((2) from the paper)
            if (IsAgentInFront(currentDirection, vecToOther, distance))
            {
                minSpacingInFront = Math.Min(minSpacingInFront, distance);
            }
        }

        // Optimal Velocity function: V(s) = min{v0, max{0, (s-l)/T}}
        var desiredSpeedV0 = traveler.PreferredSpeed;
        var speed = Math.Min(desiredSpeedV0, Math.Max(0, (minSpacingInFront - PedestrianDiameterL) / TimeGapT));

        // --- 2. Determine Direction e_i based on repulsion from ALL neighbors ---
        var desiredDirection = ToLocalVector(traveler.Position, targetPosition).Normalized();
        var totalRepulsion = Vector2D.Zero;

        foreach (var other in others)
        {
            var vecFromOther = ToLocalVector(other.Position, traveler.Position); // e_ij in paper
            var distance = vecFromOther.Magnitude();
            if (distance < 1e-6) continue;

            // Repulsion function: R(s) = a * exp((l-s)/D)
            var repulsionMagnitude = RepulsionA * Math.Exp((PedestrianDiameterL - distance) / RepulsionD);
            totalRepulsion += vecFromOther.Normalized() * repulsionMagnitude;
        }

        // e_i = N * (e0 + sum(R(s_ij) * e_ij))
        var newDirection = (desiredDirection + totalRepulsion).Normalized();

        // --- 3. Calculate new velocity and position ---
        var newVelocity = newDirection * speed;

        traveler.CurrentVelocity.X = newVelocity.X;
        traveler.CurrentVelocity.Y = newVelocity.Y;

        var displacement = newVelocity * dt;

        // --- 4. Convert back to coordinates ---
        var distanceMeters = displacement.Magnitude();
        if (distanceMeters < 1e-6)
        {
            return currentPosition;
        }

        var bearing = Math.Atan2(displacement.X, displacement.Y) * (180.0 / Math.PI);
        var newPosition =
            MarketCoordinateConversionUtils.CalculateDestination(currentPosition, bearing, distanceMeters);

        if (!PolygonUtils.IsPointInPolygon(newPosition.X, newPosition.Y, marketBoundary))
        {
            traveler.CurrentVelocity.X = 0;
            traveler.CurrentVelocity.Y = 0;
            return currentPosition;
        }

        return newPosition;
    }

    /// <summary>
    /// Checks if another agent is in front of the agent.
    /// Implements the conditions from Equation (2) in the paper.
    /// </summary>
    /// <param name="currentDirection">The direction vector of the current agent.</param>
    /// <param name="vecToOther">The vector pointing from the current agent to the other agent.</param>
    /// <param name="distance">The distance between the two agents.</param>
    private bool IsAgentInFront(Vector2D currentDirection, Vector2D vecToOther, double distance)
    {
        if (currentDirection.MagnitudeSq() < 1e-9 || distance < 1e-9)
        {
            return false;
        }

        var vecFromOther = vecToOther * -1.0; // e_ij in the paper (vector from other agent to self)

        // 1) e_i · e_ij <= 0
        if (Vector2D.Dot(currentDirection, vecFromOther) > 0)
        {
            return false;
        }

        // 2) |e_i_perp · e_ij| < l/s_ij
        var directionPerp = new Vector2D(-currentDirection.Y, currentDirection.X);
        if (Math.Abs(Vector2D.Dot(directionPerp, vecFromOther.Normalized())) >= PedestrianDiameterL / distance)
        {
            return false;
        }

        return true;
    }

    #region Helper Methods (not interesting for the model)

    private Vector2D ToLocalVector(Position origin, Position target)
    {
        var dLon = target.X - origin.X;
        var dLat = target.Y - origin.Y;
        var latRad = origin.Y * (Math.PI / 180.0);
        var x = dLon * (111320.0 * Math.Cos(latRad));
        var y = dLat * 110574.0;
        return new Vector2D(x, y);
    }

    private struct Vector2D
    {
        public double X, Y;
        public static readonly Vector2D Zero = new(0, 0);

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Magnitude() => Math.Sqrt(X * X + Y * Y);
        public double MagnitudeSq() => X * X + Y * Y;

        public Vector2D Normalized()
        {
            var mag = Magnitude();
            return mag > 1e-9 ? new Vector2D(X / mag, Y / mag) : Zero;
        }

        public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);
        public static double Dot(Vector2D a, Vector2D b) => a.X * b.X + a.Y * b.Y;
    }

    #endregion
}