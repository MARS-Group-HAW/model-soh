using Mars.Interfaces.Environments;
using SOHModel.ChristmasMarket.Agents;
using SOHModel.ChristmasMarket.Utils;
using SOHModel.Multimodal.Model;

namespace SOHModel.ChristmasMarket.MovementModels;

/// <summary>
/// Implements a pedestrian movement model based on the "Optimal Steps Model".
/// This model simulates decision-making by evaluating a discrete set of potential next steps
/// around the agent. Each potential step is assigned a "cost" or "potential" based on:
/// 1. Attractive Potential: How much closer the step moves the agent to its target.
/// 2. Pedestrian Repulsive Potential: The cost of moving too close to other agents.
/// 3. Obstacle Repulsive Potential: The cost of moving too close to boundaries.
/// The agent then chooses the step with the lowest total potential.
/// The parameters are based on values suggested in the academic paper.
/// </summary>
public class OptimalStepsMovementModel : IPedestrianMovementModel
    {
        // -- Model Parameters (from the paper, Sections II, V ) --

        // Step decision parameters
        private const int NumCirclePointsQ = 18; // (q) Number of possible positions on the circle
        private static readonly Random Random = new();

        // Step length regression parameters (Eq. 6)
        private const double Beta0 = 0.462;
        private const double Beta1 = 0.235;

        // Pedestrian repulsive potential parameters (Eq. 1)
        private const double MuP = 1000.0;  
        private const double Gp = 0.4;      
        private const double Vp = 0.4;
        private const double Ap = 1.0;
        private const double Hp = 1.0;      

        // Obstacle repulsive potential parameters (Eq. 2)
        private const double MuO = 10000.0; 
        private const double Vo = 0.2;
        private const double Ao = 3.0;
        private const double Ho = 6.0; 

        /// <summary>
        /// Calculates the next position for a market traveler using the Optimal Steps Model.
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

            // -- 1. Determine Step Length (r) based on desired speed --
            var stepLengthR = Beta0 + Beta1 * traveler.PreferredSpeed;

            // ---2. Generate Candidate Positions --
            var candidates = new List<Position> { currentPosition };
            var angleIncrement = 2 * Math.PI / NumCirclePointsQ;

            for (int k = 0; k < NumCirclePointsQ; k++)
            {
                // Add random disturbance (Eq. 4)
                var randomDisturbanceU = Random.NextDouble();
                var angle = angleIncrement * (k + randomDisturbanceU);

                var dx = stepLengthR * Math.Cos(angle);
                var dy = stepLengthR * Math.Sin(angle);
                var bearing = Math.Atan2(dx, dy) * (180.0 / Math.PI);
                var candidatePos = MarketCoordinateConversionUtils.CalculateDestination(currentPosition, bearing, stepLengthR);
                candidates.Add(candidatePos);
            }

            // -- 3. Evaluate Potential for Each Candidate and find the best one --
            double minPotential = double.MaxValue;
            Position bestPosition = currentPosition;

            var nearbyList = nearbyTravelers.ToList();

            foreach (var candidate in candidates)
            {
                if (!PolygonUtils.IsPointInPolygon(candidate.X, candidate.Y, marketBoundary))
                {
                    continue;
                }

                double currentPotential = 0;

                // a) Attractive Potential
                currentPotential += CalculateAttractivePotential(candidate, targetPosition);

                // b) Pedestrian Repulsive Potential
                foreach (var other in nearbyList)
                {
                    currentPotential += CalculatePedestrianRepulsivePotential(candidate, other);
                }

                // c) Obstacle Repulsive Potential
                currentPotential += CalculateBoundaryRepulsivePotential(candidate, marketBoundary);

                if (currentPotential < minPotential)
                {
                    minPotential = currentPotential;
                    bestPosition = candidate;
                }
            }

            var localDisplacement = ToLocalVector(currentPosition, bestPosition);
            traveler.CurrentVelocity.X = localDisplacement.X / dt;
            traveler.CurrentVelocity.Y = localDisplacement.Y / dt;

            return bestPosition;
        }

        /// <summary>
        /// Calculates the attractive potential of a candidate position.
        /// </summary>
        /// <param name="candidate">The agent's position.</param>
        /// <param name="target">The agent's target position.</param>
        /// <returns>The attractive potential value.</returns>
        private double CalculateAttractivePotential(Position candidate, Position target)
        {
            return candidate.DistanceInMTo(target);
        }

        /// <summary>
        /// Calculates the repulsive potential caused by another pedestrian.
        /// </summary>
        /// <param name="candidate">The agent's position.</param>
        /// <param name="other">The other market traveler.</param>
        /// <returns>The pedestrian repulsive potential value.</returns>
        private double CalculatePedestrianRepulsivePotential(Position candidate, MarketTraveler other)
        {
            var distance = candidate.DistanceInMTo(other.Position);

            if (distance <= Gp) return MuP;
            if (distance < Gp + Hp)
            {
                // (1): v_p * exp(-a_p * (distance - g_p))
                return Vp * Math.Exp(-Ap * (distance - Gp));
            }
            return 0;
        }

        /// <summary>
        /// Calculates the repulsive potential caused by the market boundaries.
        /// </summary>
        /// <param name="candidate">The agent's position.</param>
        /// <param name="boundary">The list of points defining the market polygon.</param>
        /// <returns>The boundary repulsive potential value.</returns>
        private double CalculateBoundaryRepulsivePotential(Position candidate, List<(double lon, double lat)> boundary)
        {
            var localBoundary = boundary.Select(p => ToLocalVector(candidate, new Position(p.lon, p.lat))).ToList();
            var nearestPointInfo = FindNearestPointOnPolygon(Vector2D.Zero, localBoundary);
            var distance = nearestPointInfo.Distance;

            // g_p / 2, as done in the paper
            var obstacleRadius = Gp / 2.0;

            if (distance <= obstacleRadius) return MuO;
            if (distance < Ho) // g_p/2 + h_o
            {
                // (2)
                return Vo * Math.Exp(-Ao * (distance - obstacleRadius));
            }
            return 0;
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

        private (Vector2D VectorToPoint, double Distance) FindNearestPointOnPolygon(Vector2D point, List<Vector2D> polygon)
        {
            var minDistanceSq = double.MaxValue;
            var nearestPoint = Vector2D.Zero;

            for (int i = 0; i < polygon.Count; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[(i + 1) % polygon.Count];
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
            if (abMagSq < 1e-12) return a;
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

            public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
            public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
            public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);
            public static double Dot(Vector2D a, Vector2D b) => a.X * b.X + a.Y * b.Y;
        }

        #endregion
    }