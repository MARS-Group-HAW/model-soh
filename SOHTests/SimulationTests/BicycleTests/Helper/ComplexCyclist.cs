using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Components.Environments;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Mathematics.Statistics;
using SOHBicycleModel.Common;
using SOHBicycleModel.Steering;
using SOHDomain.Common;
using SOHDomain.Model;
using SOHDomain.Output;
using SOHDomain.Steering.Common;

namespace SOHTests.SimulationTests.BicycleTests.Helper
{
    public class ComplexCyclist : IAgent, ISpatialGraphEntity, ITripSavingAgent, IBicycleSteeringCapable
    {
        private static int _stableId;

        private readonly WiedemannAccelerator _accelerator;
        private readonly SimulationContext _context;
        private readonly ISpatialGraphEnvironment _graphEnvironment;
        private Position _position;

        public ComplexCyclist(SimulationContext context, double pos, double power, double weight, double width,
            ISpatialGraphEnvironment graphEnvironment)
        {
            CyclingPower = power;
            Mass = weight;
            Width = width;
            Acceleration = 0;
            ID = Guid.NewGuid();
            _context = context;
            _graphEnvironment = graphEnvironment;
            _accelerator = new WiedemannAccelerator(this);
//            _accelerationOld  = new W99();
            var max = MaxSpeed > 0 ? MaxSpeed : NormalDist.NormalDistRandom(7, 1.5);

            var edge = graphEnvironment.Edges.First();
//            var edge2 = graphEnvironment.Edges.ToArray()[1];
            var lane = RandomHelper.Random.Next(edge.Value.LaneCount);

            Route = CreateRoute(edge.Value);

            if (!graphEnvironment.Insert(this, edge.Value, pos, lane))
                throw new ApplicationException("The insertion on the edge was not possible");

            Trips = new List<(ModalType, List<TripPosition>)>();
            CalculateNewPositionAndBearing();
        }

        public Route Route { get; }

        public ISpatialGraphEntity DriverAhead { get; set; }

        public ISpatialGraphEntity DriverBehind { get; set; }

//        public double SpeedChange { get; set; }
        public double Speed { get; set; }
        public double Acceleration { get; set; }

        public double Width { get; set; }
        public double MaxSpeed { get; set; } = NormalDist.NormalDistRandom(7, 1.5);

        public double DistanceAhead { get; set; }

        public double Bearing { get; set; }

        private bool switchedLaneRecently { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }
        public int DesiredLane { get; set; }

        public Guid ID { get; set; }

        public void Tick()
        {
            switchedLaneRecently = false;
            var exploreResult = _graphEnvironment.Explore(this, Route, 1000);
            var result = exploreResult.EdgeExplores.First(edgeExploreResult =>
            {
                if (edgeExploreResult.LaneExplores.TryGetValue(LaneOnCurrentEdge, out var lane))
                    return lane.Forward.Count > 0;

                return false;
            });

            // Is another driver ahead?
            DriverAhead = result.LaneExplores.TryGetValue(LaneOnCurrentEdge, out var laneExploreResult)
                ? laneExploreResult.Forward.FirstOrDefault()
                : null;

            DriverBehind = result.LaneExplores.TryGetValue(LaneOnCurrentEdge, out var laneBackwardExploreResult)
                ? laneBackwardExploreResult.Backward.FirstOrDefault()
                : null;


            if (DriverAhead != null && DriverAhead is ComplexCyclist driver)
            {
                if (driver.PositionOnCurrentEdge < PositionOnCurrentEdge)
                    DistanceAhead = CurrentEdge.Length - PositionOnCurrentEdge + driver.PositionOnCurrentEdge -
                                    driver.Length;
                else
                    DistanceAhead = driver.PositionOnCurrentEdge - driver.Length - PositionOnCurrentEdge;

                var overtake = false;
                var maxSpeedDiff = Math.Abs(BicycleConstants.MaxDecelFactor) / 2;

                var breakingDistance = Speed * Speed / (2 * BicycleConstants.MeanDecel);
                if (MaxSpeed - driver.Speed > maxSpeedDiff &&
                    DistanceAhead < breakingDistance * 3)
                    //                    overtake = HandleDriverType.DecideIfOvertaking(DriverType.Normal);
                    overtake = true;

                if (overtake)
                    HandleSimpleOvertaking();
                else
                    Acceleration =
                        _accelerator.CalculateSpeedChange(Speed, driver.Speed, DistanceAhead, driver.Acceleration,
                            Acceleration, MaxSpeed);
            }
            else
            {
//                SpeedChange = CalculateSpeedChange(Velocity, MaxSpeed, Route.RemainingRouteDistanceToGoal, 0);
                Acceleration =
                    _accelerator.CalculateSpeedChange(Speed, 0, Route.RemainingRouteDistanceToGoal, 0,
                        Acceleration, MaxSpeed);
            }

            Speed += Acceleration;
            if (Speed < 0) Speed = 0;

            //            if (Route.Count <= 1)
//            {
//                Route = CreateRoute(CurrentEdge);
//            }

            if (CurrentEdge == null)
                throw new ApplicationException(
                    "Actions before movement has result into a null assignment of the current entity edge");

            if (double.IsNegativeInfinity(Speed))
                throw new ApplicationException("The speed is negative infinity, should not happen");

            if (double.IsInfinity(Speed)) throw new ApplicationException("The speed is infinity, should not happen");

            if (double.IsNaN(Speed)) throw new ApplicationException("The speed is NaN, should not happen");

            if (_graphEnvironment.Move(this, Route, Speed)) CalculateNewPositionAndBearing();
        }

        public void Notify(PassengerMessage passengerMessage)
        {
            throw new NotImplementedException();
        }

        public double DriverRandom { get; }
        public DriverType DriverType { get; }
        public double CyclingPower { get; }
        public double Mass { get; }
        public double Gradient => 0;

        public Position Position
        {
            get => _position;
            set
            {
                Latitude = value.Latitude;
                Longitude = value.Longitude;
                var clock = _context.CurrentTimePoint.GetValueOrDefault();
                var time = (int) clock.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                if (!Trips.Any()) Trips.Add((ModalType.Cycling, new List<TripPosition>()));
                Trips.Last().Item2.Add(new TripPosition(Longitude, Latitude) {UnixTimestamp = time});

                _position = value;
            }
        }

        public double Length { get; }

        public ISpatialEdge CurrentEdge { get; set; }

        public double PositionOnCurrentEdge { get; set; }
        public int LaneOnCurrentEdge { get; set; }

        /// <summary>
        ///     Gets or sets the flag indicating that the entity is moving on an opposite edge.
        /// </summary>
        public bool IsWrongWayDriving { get; set; }

        /// <summary>
        ///     Gets the modal type restricting the entity how and where it can move on.
        /// </summary>
        public SpatialModalityType ModalityType => SpatialModalityType.Cycling;

        /// <summary>
        ///     Gets the flag, indicating that moving operations of this
        ///     entity are checked for collision with other ones.
        /// </summary>
        public bool IsCollidingEntity => true;

        public int StableId { get; } = _stableId++;
        
        public TripsCollection TripsCollection { get; }
        
        public List<(ModalType, List<TripPosition>)> Trips { get; }

        private void HandleSimpleOvertaking()
        {
//            if ((((SpatialEdge) Route.Stops[0].Edge).LaneCount <= 1))
//            {
//                double pathWidth = (1.8 + 1.925) / 2;
//
//                if (pathWidth >= Width * 2)
//                {
//                    // TODO "beam" over next bicycle
////                    _bicycle.SetPosition(Route.Stops[0].Edge, _bicycle.PositionOnCurrentEdge + 2,
////                       Route.Stops[0].DrivingLane);
////                    SpatialGraphMovementResult movementResult =
////                    Environment.Move(_bicycle, Route, DesiredDrivingDistance + 2, true);
//                    //                    ProcessMoveResult(movementResult);
//                }
//            }
//            else
//            {
            var leftLaneToCheck = LaneOnCurrentEdge + 1;
            var rightLaneToCheck = LaneOnCurrentEdge - 1;

            var leftLaneClear = laneClear(leftLaneToCheck);
            var rightLaneClear = laneClear(rightLaneToCheck);

            if (leftLaneClear)
            {
                switchedLaneRecently = true;
                DesiredLane = leftLaneToCheck;
            }
            else if (rightLaneClear) // TODO && check if on street or 
            {
                switchedLaneRecently = true;
                DesiredLane = rightLaneToCheck;
            }

//            }
        }

        private bool laneClear(int lane)
        {
            var maxSpeedDiff = Math.Abs(BicycleConstants.MaxDecelFactor) - 1;

            var ownBreakingDistance = Speed * Speed / (2 * BicycleConstants.MeanDecel);
            if (lane < 0 || lane > 1) return false;

            if (DriverAhead != null && DriverAhead is ComplexCyclist driverAead)
                if (driverAead.LaneOnCurrentEdge == lane)
                    if (MaxSpeed - driverAead.Speed > maxSpeedDiff ||
                        DistanceAhead < ownBreakingDistance * 3)
                        return false;

            if (DriverBehind != null && DriverBehind is ComplexCyclist driverBehind)
            {
                double distToDriverBehind = 0;
                if (driverBehind.PositionOnCurrentEdge < PositionOnCurrentEdge)
                    distToDriverBehind = CurrentEdge.Length - PositionOnCurrentEdge +
                                         driverBehind.PositionOnCurrentEdge -
                                         driverBehind.Length;
                else
                    distToDriverBehind =
                        driverBehind.PositionOnCurrentEdge - driverBehind.Length - PositionOnCurrentEdge;

                if (driverBehind.LaneOnCurrentEdge == lane)
                    if (driverBehind.Speed - Speed >= maxSpeedDiff ||
                        distToDriverBehind < ownBreakingDistance * 3)
                        // vehicle behind too fast | too close
                        return false;
            }

            return true;
        }

        private Route CreateRoute(ISpatialEdge start)
        {
//            var first  = start.To.OutgoingEdges.First();
//            var second = first.To.OutgoingEdges.First();
//            var third  = second.To.OutgoingEdges.First();

//            return new Route
//            {
//                {start, LaneOnCurrentEdge}, {first, LaneOnCurrentEdge}, {second, LaneOnCurrentEdge},
//                {third, LaneOnCurrentEdge}
//            };
            return new Route {{start, LaneOnCurrentEdge}};
        }

        private void CalculateNewPositionAndBearing()
        {
            var currentEdge = Route[0].Edge;
            if (currentEdge.Geometry == null || !currentEdge.Geometry.Any())
            {
                Bearing = currentEdge.From.Position.GetBearing(currentEdge.To.Position);
                Position = currentEdge.From.Position.GetRelativePosition(Bearing, PositionOnCurrentEdge);
            }
            else
            {
                var samplingPoints = new List<Position> {currentEdge.From.Position};
                samplingPoints.AddRange(currentEdge.Geometry);
                samplingPoints.Add(currentEdge.To.Position);

                var lastPoint = samplingPoints[0];
                var distanceCalculatedSoFar = 0.0;
                for (var i = 1; i < samplingPoints.Count; i++)
                {
                    var distance = lastPoint.DistanceInKmTo(samplingPoints[i]) * 1000;

                    if (distanceCalculatedSoFar + distance > PositionOnCurrentEdge)
                    {
                        Bearing = lastPoint.GetBearing(samplingPoints[i]);
                        var distanceToMove = PositionOnCurrentEdge - distanceCalculatedSoFar;
                        Position = lastPoint.GetRelativePosition(Bearing, distanceToMove);
                        break;
                    }

                    lastPoint = samplingPoints[i];
                    distanceCalculatedSoFar += distance;

                    if (DesiredLane == 1 && switchedLaneRecently)
                    {
                        if (Position.Latitude + 10 > 90)
                            Position.Latitude = 90;
                        else
                            Position.Latitude = Position.Latitude + 10;

                        if (Position.Longitude + 10 > 90)
                            Position.Longitude = 90;
                        else
                            Position.Longitude = Position.Longitude + 10;
                    }
                    else if (DesiredLane == 0 && switchedLaneRecently)
                    {
                        Position.Latitude = Position.Latitude - 10;
                        Position.Longitude = Position.Longitude - 10;
                    }
                }
            }
        }

        public bool OvertakingActivated { get; }
    }
}