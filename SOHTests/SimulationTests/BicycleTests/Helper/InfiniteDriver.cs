using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Components.Environments;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Mathematics.Statistics;
using SOHDomain.Model;
using SOHDomain.Output;

namespace SOHTests.SimulationTests.BicycleTests.Helper
{
    public class InfiniteDriver : IAgent, ISpatialGraphEntity, ITripSavingAgent
    {
        private const double SafeTimeHeadway = 1.6; //seconds
        private const double MaxAcceleration = 0.73; //meter per square second
        private const double ComfortableDeceleration = 1.67; //meter per square second
        private const double GapInCongestion = 2.0; //meter
        private const double GapInConvoy = 0.0; //meter
        private const int AccelerationExponent = 4;
        private static int _stableId;
        private readonly SimulationContext _context;
        private readonly ISpatialGraphEnvironment _graphEnvironment;
        private Position _position;

        public InfiniteDriver(SimulationContext context, double pos, ISpatialGraphEnvironment graphEnvironment)
        {
            ID = Guid.NewGuid();
            _context = context;
            _graphEnvironment = graphEnvironment;

            var edge = graphEnvironment.Edges.First().Value;
            var lane = RandomHelper.Random.Next(edge.LaneCount);
            MaxSpeed = MaxSpeed > 0 ? MaxSpeed : 15;

            Route = CreateRoute(edge);

            if (!graphEnvironment.Insert(this, edge, pos, lane))
                throw new ApplicationException("The insertion on the edge was not possible");

            Trips = new List<(ModalType, List<TripPosition>)>();
            CalculateNewPositionAndBearing();
        }

        public Route Route { get; private set; }

        public ISpatialGraphEntity DriverAhead { get; set; }
        public double Acceleration { get; set; }

        public double Speed { get; set; }
        public double MaxSpeed { get; set; } = RandomHelper.Random.Next(45, 65);

        public double DistanceAhead { get; set; }

        public double Bearing { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public void Tick()
        {
            var exploreResult = _graphEnvironment.Explore(this, Route, 1000);
            var result = exploreResult.EdgeExplores.First(edgeExploreResult =>
            {
                if (edgeExploreResult.LaneExplores.TryGetValue(LaneOnCurrentEdge, out var lane))
                    return lane.Forward.Count > 0;

                return false;
            });

            // Is another driver ahead?
            DriverAhead = result.LaneExplores.TryGetValue(LaneOnCurrentEdge, out var laneExploreResult)
                ? laneExploreResult.Forward.FirstOrDefault() : null;

            if (DriverAhead != null)
            {
                if (DriverAhead is InfiniteCyclist)
                {
                    var driver = (InfiniteCyclist) DriverAhead;
                    if (driver.PositionOnCurrentEdge < PositionOnCurrentEdge)
                        DistanceAhead = CurrentEdge.Length - PositionOnCurrentEdge + driver.PositionOnCurrentEdge -
                                        driver.Length;
                    else
                        DistanceAhead = driver.PositionOnCurrentEdge - driver.Length - PositionOnCurrentEdge;

                    Acceleration = CalculateAcceleration(Speed, MaxSpeed,
                        DistanceAhead, driver.Speed);
                }
                else if (DriverAhead is InfiniteDriver)
                {
                    var driver = (InfiniteDriver) DriverAhead;
                    if (driver.PositionOnCurrentEdge < PositionOnCurrentEdge)
                        DistanceAhead = CurrentEdge.Length - PositionOnCurrentEdge + driver.PositionOnCurrentEdge -
                                        driver.Length;
                    else
                        DistanceAhead = driver.PositionOnCurrentEdge - driver.Length - PositionOnCurrentEdge;

                    Acceleration = CalculateAcceleration(Speed, MaxSpeed,
                        DistanceAhead, driver.Speed);
                }
            }
            else
            {
                Acceleration = CalculateAcceleration(Speed, MaxSpeed, Route.RemainingRouteDistanceToGoal, 0);
            }

            Speed += Acceleration;
            if (Speed < 0) Speed = 0;

            if (Route.Count <= 1) Route = CreateRoute(CurrentEdge);

            if (CurrentEdge == null)
                throw new ApplicationException(
                    "Actions before movement has result into a null assignment of the current entity edge");

            if (double.IsNegativeInfinity(Speed))
                throw new ApplicationException("The speed is negative infinity, should not happen");

            if (double.IsInfinity(Speed)) throw new ApplicationException("The speed is infinity, should not happen");

            if (double.IsNaN(Speed)) throw new ApplicationException("The speed is NaN, should not happen");

            if (_graphEnvironment.Move(this, Route, Speed)) CalculateNewPositionAndBearing();
        }

        public Guid ID { get; set; }

        public Position Position
        {
            get => _position;
            set
            {
                Latitude = value.Latitude;
                Longitude = value.Longitude;
                var clock = _context.CurrentTimePoint.GetValueOrDefault();
                var time = (int) clock.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                if (!Trips.Any()) Trips.Add((ModalType.CarDriving, new List<TripPosition>()));
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

        private Route CreateRoute(ISpatialEdge start)
        {
            var first = start.To.OutgoingEdges.First().Value;
            var second = first.To.OutgoingEdges.First().Value;
            var third = second.To.OutgoingEdges.First().Value;

            return new Route
            {
                {start, LaneOnCurrentEdge}, {first, LaneOnCurrentEdge}, {second, LaneOnCurrentEdge},
                {third, LaneOnCurrentEdge}
            };
        }

        public static double CalculateAcceleration(double currentSpeed, double maxSpeed, double distanceToCarAhead,
            double speedCarAhead)
        {
            var speedDiff = Math.Round(Math.Abs(speedCarAhead - currentSpeed), 3);
            var desiredGap = Math.Round(GapInCongestion + GapInConvoy * Math.Sqrt(currentSpeed / maxSpeed) +
                                        SafeTimeHeadway * currentSpeed +
                                        currentSpeed * speedDiff /
                                        (2 * Math.Sqrt(MaxAcceleration * ComfortableDeceleration)), 3);
            var Acceleration = Math.Round(MaxAcceleration *
                                          (1 - Math.Pow(currentSpeed / maxSpeed, AccelerationExponent) -
                                           Math.Pow(desiredGap / distanceToCarAhead, 2)), 3);

            return Acceleration;
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
                }
            }
        }
    }
}