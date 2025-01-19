using System;
using System.Linq;
using Mars.Components.Environments;
using Mars.Core.Data.Wrapper.Memory;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHModel.Car.Model;
using SOHModel.Car.Steering;
using SOHModel.Domain.Steering.Common;
using SOHModel.Multimodal.Output.Trips;
using Xunit;

namespace SOHTests.Commons.Agent;

/// <summary>
///     A car driver that drives as long as the simulations goes in a circle.
/// </summary>
public class InfiniteSteeringDriver : IAgent, ICarSteeringCapable, ITripSavingAgent
{
    private static int _stableId;
    private readonly ISpatialEdge _edge;
    private readonly CarSteeringHandle _steering;

    /// <summary>
    ///     The amount of rounds that this driver completes over the time.
    /// </summary>
    public int RoundsFinished;

    public InfiniteSteeringDriver(ISimulationContext context, double pos, ISpatialGraphEnvironment graphEnvironment,
        int lane = 0, double maxSpeed = -1)
    {
        ID = Guid.NewGuid();

        _edge = graphEnvironment.Edges.First().Value;
        var route = CreateRoute(_edge, lane);

        Car = Golf.Create(graphEnvironment);
        Car.Position = Car.CalculateNewPositionFor(route, out _);
        Car.IsCollidingEntity = true;
        if (maxSpeed > 0)
            Car.MaxSpeed = maxSpeed;

        if (!graphEnvironment.Insert(Car, _edge, pos, lane))
            throw new ApplicationException("The insertion on the edge was not possible");

        TripsCollection = new TripsCollection(context);

        Assert.True(Car.TryEnterDriver(this, out _steering));
        Assert.NotNull(_steering);
        _steering.Route = route;
    }

    public double Velocity => Car.Velocity;
    public ISpatialEdge CurrentEdge => Car.CurrentEdge;
    public double PositionOnCurrentEdge => Car.PositionOnCurrentEdge;
    public int LaneOnCurrentEdge => Car?.LaneOnCurrentEdge ?? 0;

    /// <summary>
    ///     Provides the total distance that this driver has covered.
    /// </summary>
    public double TotalDistanceDriven => CurrentEdge.Length * RoundsFinished + PositionOnCurrentEdge;

    public double MaxSpeed
    {
        get => Car.MaxSpeed;
        set => Car.MaxSpeed = value;
    }

    public void Tick()
    {
        if (_steering.Route.Count < 3) _steering.Route = CreateRoute(_edge, LaneOnCurrentEdge);

        var previousPosition = PositionOnCurrentEdge;
        _steering.Move();

        if (PositionOnCurrentEdge < previousPosition) RoundsFinished++;

        TripsCollection.Add(new[] { (object)1 }, Position);
    }

    public Guid ID { get; set; }

    public Position Position
    {
        get => Car.Position;
        set => throw new ApplicationException("Should not be called.");
    }

    public void Notify(PassengerMessage passengerMessage)
    {
        throw new NotImplementedException();
    }

    public Car Car { get; }
    public bool OvertakingActivated { get; set; }
    public bool BrakingActivated { get; set; }
    public bool CurrentlyCarDriving => true;

    public int StableId { get; } = _stableId++;
    public TripsCollection TripsCollection { get; }

    private static Route CreateRoute(ISpatialEdge start, int lane)
    {
        var first = start.To.OutgoingEdges.First().Value;
        var second = first.To.OutgoingEdges.First().Value;
        var third = second.To.OutgoingEdges.First().Value;

        return new Route
        {
            { start, lane }, { first, lane }, { second, lane }, { third, lane }
        };
    }
}