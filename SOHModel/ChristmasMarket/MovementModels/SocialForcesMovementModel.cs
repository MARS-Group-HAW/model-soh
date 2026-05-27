using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.ChristmasMarket.Utils;

namespace SOHModel.ChristmasMarket.MovementModels;

/// <summary>
/// Implements a pedestrian movement model based on the classic "Social Force Model".
/// This model treats pedestrians as particles that are subject to a combination of forces:
/// 1. Driving Force: Pulls the agent towards its destination.
/// 2. Pedestrian Repulsive Force: Pushes agents away from each other to avoid collisions.
/// 3. Boundary Repulsive Force: Pushes agents away from walls and obstacles.
/// The parameters are based on values suggested in the academic paper.
/// </summary>
public class SocialForcesMovementModel : IPedestrianMovementModel
{
    // -- Model Parameters (paper + optimization for MARS/SOH) --

    // Driving Force
    private const double RelaxationTime = 0.5;

    // Pedestrian Repulsion
    private const double PedestrianRepulsionA = 2000.0; // Assumed strength of the force
    private const double PedestrianRepulsionB = 0.08;  // Assumed range of the force
    private const double AgentRadius = 0.35;           // Assumed radius of a pedestrian

    // Boundary Repulsion
    private const double WallRepulsionA = 3000.0;
    private const double WallRepulsionB = 0.05;

    // Agent Properties
    private const double AgentMass = 80.0; // in kg

    /// <summary>
    /// Calculates the next position for a market traveler using the Social Force Model.
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

        // -- 1. Calculate Forces --
        var totalForce = new Vector2D(0, 0);

        // a) Driving Force
        totalForce += CalculateDrivingForce(traveler, targetPosition);

        // b) Pedestrian Repulsive Force
        if (nearbyTravelers != null)
        {
            foreach (var other in nearbyTravelers)
            {
                totalForce += CalculatePedestrianRepulsiveForce(traveler, other);
            }
        }

        // c) Boundary Repulsive Force
        if (marketBoundary != null && marketBoundary.Count > 2)
        {
            totalForce += CalculateBoundaryRepulsiveForce(traveler, marketBoundary);
        }

        // -- 2. Update Velocity and Position --
        var currentVelocity = new Vector2D(traveler.CurrentVelocity.X, traveler.CurrentVelocity.Y);

        // F = m*a  =>  a = F/m
        var acceleration = totalForce / AgentMass;

        // v_new = v_old + a * dt
        var newVelocity = currentVelocity + acceleration * dt;

        var maxSpeed = traveler.PreferredSpeed * 1.3;
        if (newVelocity.Magnitude() > maxSpeed)
        {
            newVelocity = newVelocity.Normalized() * maxSpeed;
        }

        traveler.CurrentVelocity.X = newVelocity.X;
        traveler.CurrentVelocity.Y = newVelocity.Y;

        // p_new = p_old + v_new * dt
        var displacement = newVelocity * dt;

        // -- 3. Convert back to coordinates and find new position --
        var distanceMeters = displacement.Magnitude();
        if (distanceMeters < 1e-6)
        {
            return currentPosition;
        }

        var bearing = Math.Atan2(displacement.X, displacement.Y) * (180.0 / Math.PI);
        var newPosition = MarketCoordinateConversionUtils.CalculateDestination(currentPosition, bearing, distanceMeters);

        if (!PolygonUtils.IsPointInPolygon(newPosition.X, newPosition.Y, marketBoundary))
        {
            traveler.CurrentVelocity.X = 0;
            traveler.CurrentVelocity.Y = 0;
            return currentPosition;
        }

        return newPosition;
    }

    /// <summary>
    /// Calculates the driving force that motivates the agent to move towards its target.
    /// </summary>
    /// <param name="traveler">The agent being moved.</param>
    /// <param name="target">The agent's target position.</param>
    /// <returns>A Vector2D representing the driving force.</returns>
    private Vector2D CalculateDrivingForce(MarketTraveler traveler, Position target)
    {
        var vecToTarget = ToLocalVector(traveler.Position, target);
        var desiredSpeed = traveler.PreferredSpeed;

        var desiredVelocity = vecToTarget.Normalized() * desiredSpeed;
        var currentVelocity = new Vector2D(traveler.CurrentVelocity.X, traveler.CurrentVelocity.Y);

        // (desired_v - current_v) / relaxation_time
        return (desiredVelocity - currentVelocity) * (AgentMass / RelaxationTime);
    }

    /// <summary>
    /// Calculates the repulsive force given by another nearby pedestrian.
    /// </summary>
    /// <param name="self">The agent whose force is being calculated.</param>
    /// <param name="other">The other agent givign the force.</param>
    /// <returns>A Vector2D representing the repulsive force.</returns>
    private Vector2D CalculatePedestrianRepulsiveForce(MarketTraveler self, MarketTraveler other)
    {
        var vecToOther = ToLocalVector(self.Position, other.Position);
        var distance = vecToOther.Magnitude();
        var sumOfRadii = AgentRadius * 2;

        if (distance >= sumOfRadii + 2.0) return Vector2D.Zero; 

        // f = A * exp((r_ij - d_ij) / B) * n_ij
        var forceMagnitude = PedestrianRepulsionA * Math.Exp((sumOfRadii - distance) / PedestrianRepulsionB);

        var direction = (vecToOther.Normalized() * -1.0);
        return direction * forceMagnitude;
    }

    /// <summary>
    /// Calculates the repulsive force given by the nearest point on the market boundary.
    /// </summary>
    /// <param name="traveler">The agent being moved.</param>
    /// <param name="boundary">The list of coordinates defining the market boundary polygon.</param>
    /// <returns>A Vector2D representing the boundary repulsive force.</returns>
    private Vector2D CalculateBoundaryRepulsiveForce(MarketTraveler traveler, List<(double lon, double lat)> boundary)
    {
        var localBoundary = boundary.Select(p => ToLocalVector(traveler.Position, new Position(p.lon, p.lat))).ToList();
        var nearestPointInfo = FindNearestPointOnPolygon(Vector2D.Zero, localBoundary);

        var distance = nearestPointInfo.Distance;
        if (distance >= AgentRadius + 2.0) return Vector2D.Zero;

        // see CalculatePedestrianRepulsiveForce()
        var forceMagnitude = WallRepulsionA * Math.Exp((AgentRadius - distance) / WallRepulsionB);
        
        var direction = (nearestPointInfo.VectorToPoint.Normalized() * -1.0);
        return direction * forceMagnitude;
    }

    #region Helper Methods and Structs
    
    private Vector2D ToLocalVector(Position origin, Position target)
    {
        var dLon = target.X - origin.X;
        var dLat = target.Y - origin.Y;
        var latRad = origin.Y * (Math.PI / 180.0);

        var x = dLon * (111320.0 * Math.Cos(latRad));
        var y = dLat * 110574.0;

        return new Vector2D(x, y);
    }

    private (Vector2D VectorToPoint, double Distance) FindNearestPointOnPolygon(Vector2D point, List<Vector2D> polygon)
    {
        var minDistanceSq = double.MaxValue;
        var nearestPoint = Vector2D.Zero;

        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count]; // Wrap around for the last segment

            var currentNearest = FindNearestPointOnLineSegment(point, p1, p2);
            var distSq = (point - currentNearest).MagnitudeSq();

            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                nearestPoint = currentNearest;
            }
        }

        return (nearestPoint - point, Math.Sqrt(minDistanceSq));
    }

    private Vector2D FindNearestPointOnLineSegment(Vector2D p, Vector2D a, Vector2D b)
    {
        var ap = p - a;
        var ab = b - a;
        var abMagSq = ab.MagnitudeSq();

        if (abMagSq < 1e-12) return a; // a and b are the same point

        var t = Vector2D.Dot(ap, ab) / abMagSq;
        
        if (t < 0.0) return a;
        if (t > 1.0) return b;
        
        return a + ab * t;
    }

    private struct Vector2D
    {
        public double X, Y;
        public static readonly Vector2D Zero = new(0, 0);

        public Vector2D(double x, double y) { X = x; Y = y; }

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
        public static Vector2D operator /(Vector2D a, double s) => new(a.X / s, a.Y / s);
        public static double Dot(Vector2D a, Vector2D b) => a.X * b.X + a.Y * b.Y;
    }

    #endregion
}