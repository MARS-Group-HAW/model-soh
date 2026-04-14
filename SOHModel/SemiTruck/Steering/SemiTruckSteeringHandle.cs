using Mars.Common;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;


namespace SOHModel.SemiTruck.Steering
{
    /// <summary>
    ///     A steering handle for moving a semi-truck along a specified route using physics-based motion.
    ///     Overrides the IDM-only movement from <see cref="VehicleSteeringHandle{,,}"/> to bound velocity
    ///     changes by the truck's <c>MaxAcceleration</c> / <c>MaxDeceleration</c> per tick and to compute
    ///     distance via trapezoidal integration -- making both the motion curve and <c>Vehicle.Acceleration</c>
    ///     physically meaningful across arbitrary tick durations.
    ///
    ///     Explanation on the "steering" flow:
    ///     1. Intended State: Figure out what speed we want to reach (v_new) based on constraints,
    ///         e.g., IDM, physical power limits.
    ///     2. Trapezoid Distance: Calculate how far the truck would move if it linearly accelerated to that speed
    ///     3. Move the vehicle
    ///     4. Back-calculate acceleration: Since we know the distance traveled over deltaT, reversing the calculation
    ///         formally gives us the true acceleration at that time.
    /// </summary>
    public class SemiTruckSteeringHandle : VehicleSteeringHandle<ISemiTruckSteeringCapable, IPassengerCapable, SemiTruckSteeringHandle, SemiTruckPassengerHandle>
    {
        public SemiTruckSteeringHandle(ISpatialGraphEnvironment environment, Model.SemiTruck semiTruck) :
            base(environment, semiTruck, semiTruck.MaxSpeed)
        {
        }

        private Model.SemiTruck SemiTruck => (Model.SemiTruck)Vehicle;

        // TODO properly inject layer
        /// <summary>Simulation tick duration in seconds.</summary>
        private double DeltaT => ((Model.SemiTruckLayer)SemiTruck.Layer)._tickDuration.TotalSeconds;

        /// <summary>Effective speed cap: lower of road speed limit and truck's own maximum.</summary>
        private double PhysicsMaxSpeed => Math.Min(SpeedLimit, Vehicle.MaxSpeed);

        // ── Distance calculation ───────────────────────────────────────────────

        /// <summary>
        ///     Converts the IDM's desired deceleration into a physics-consistent driving distance.
        ///     The IDM result is treated as a *target* velocity hint; actual velocity change per tick is
        ///     bounded by <c>MaxAcceleration</c> / <c>MaxDeceleration</c> × deltaT.
        ///     Distance is computed via trapezoidal integration: d = (v_0 + v_new) / 2 × deltaT.
        /// </summary>
        protected override double CalculateDrivingDistance(double biggestDeceleration)
        {
            if (Route.RemainingRouteDistanceToGoal < 3)
                return Route.RemainingRouteDistanceToGoal;

            var v0      = Vehicle.Velocity;
            var vTarget = ResolveTargetVelocity(biggestDeceleration);
            var vNew    = ApplyPhysicsConstraints(v0, vTarget);
            return KinematicDistance(v0, vNew);
        }

        /// <summary>
        ///     Converts <paramref name="biggestDeceleration"/> (the IDM's recommended speed change this tick)
        ///     into a target velocity the truck is aiming for.
        ///     In free-driving mode (<paramref name="biggestDeceleration"/> == <c>MaximalDeceleration</c>)
        ///     the target is the current speed limit.
        /// </summary>
        private double ResolveTargetVelocity(double biggestDeceleration)
        {
            if (biggestDeceleration >= MaximalDeceleration)
                return PhysicsMaxSpeed; // free driving -- accelerate towards speed limit

            return Math.Max(0, Vehicle.Velocity + biggestDeceleration);
        }

        /// <summary>
        ///     Clamps the desired velocity change to what is physically achievable in one tick
        ///     given <c>MaxAcceleration</c> and <c>MaxDeceleration</c>.
        /// </summary>
        private double ApplyPhysicsConstraints(double v0, double vTarget)
        {
            var deltaT  = DeltaT;
            var maxDec  = SemiTruck.MaxDeceleration * deltaT;
            var maxAcc  = SemiTruck.MaxAcceleration * deltaT;
            var clamped = Math.Clamp(vTarget - v0, -maxDec, maxAcc);
            return Math.Max(0, v0 + clamped);
        }

        /// <summary>Trapezoidal integration: d = (v_0 + v_new) / 2 × deltaT.</summary>
        private double KinematicDistance(double v0, double vNew)
            => (v0 + vNew) / 2.0 * DeltaT;

        // ── Move execution ─────────────────────────────────────────────────────

        /// <summary>
        ///     Executes the physical move using the pre-computed <paramref name="physicsDistance"/> (metres).
        ///     Sets <c>Vehicle.Velocity</c> to the kinematically consistent new speed and
        ///     <c>Vehicle.Acceleration</c> to the true m/s^2 value
        /// </summary>
        protected override void PerformMoveAction(double physicsDistance)
        {
            var v0 = Vehicle.Velocity;

            if (physicsDistance > 0)
            {
                if (Environment.Move(Vehicle, Route, physicsDistance))
                {
                    var vNew = KinematicVelocityFromDistance(v0, physicsDistance);
                    Vehicle.Velocity     = Math.Round(vNew, 2);
                    Vehicle.Acceleration = (Vehicle.Velocity - v0) / DeltaT;
                    SemiTruck.Position = SemiTruck.CalculateNewPositionFor(Route, out var bearing);
                    SemiTruck.Bearing  = bearing;
                }

                if (Vehicle.CurrentEdge != Route.Stops.First().Edge)
                    PlanDesiredLanesForNextMoves();
            }
            else
            {
                Vehicle.Acceleration = -v0 / DeltaT;
                Vehicle.Velocity     = 0;
            }

            if (GoalReached) Vehicle.Velocity = 0;
        }

        /// <summary>
        ///     Inverts the trapezoidal distance formula to recover new velocity:
        ///     d = (v_0 + v_new) / 2 × deltaT  →  v_new = 2d / deltaT − v_0
        /// </summary>
        private double KinematicVelocityFromDistance(double v0, double physicsDistance)
            => Math.Max(0, 2.0 * physicsDistance / DeltaT - v0);
    }
}
