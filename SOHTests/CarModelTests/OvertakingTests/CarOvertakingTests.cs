using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model.Options;
using SOHModel.Bicycle.Model;
using SOHModel.Car.Model;
using SOHModel.Car.Steering;
using SOHModel.Domain.Steering.Common;
using Xunit;

namespace SOHTests.CarModelTests.OvertakingTests;

public class CarOvertakingTests
{
    private readonly Position _goalPosition = Position.CreatePosition(10.011156, 53.522961);
    private readonly Position _startPosition = Position.CreatePosition(9.981279, 53.527625);

    [Fact]
    public void SwitchLane()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphVeddelerDamm);
        var startNode = environment.NearestNode(_startPosition);
        var goalNode = environment.NearestNode(_goalPosition);

        var slowAgent = new OvertakingAgent(environment, startNode, goalNode, 50, 5);
        Assert.InRange(slowAgent.PositionOnCurrentEdge, 50, 50);
        Assert.Equal(0, slowAgent.CurrentLane);

        var fastAgent = new OvertakingAgent(environment, startNode, goalNode, 0, 10);
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 0, 0);
        Assert.Equal(0, fastAgent.CurrentLane);

        fastAgent.Tick();
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 9.5, 10);
        Assert.Equal(1, fastAgent.CurrentLane);

        fastAgent.Tick();
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 19, 20);
        Assert.Equal(1, fastAgent.CurrentLane);

        fastAgent.Tick();
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 28, 30);
        Assert.Equal(1, fastAgent.CurrentLane);

        fastAgent.Tick();
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 37, 40);
        Assert.Equal(1, fastAgent.CurrentLane);

        fastAgent.Tick();
        Assert.InRange(fastAgent.PositionOnCurrentEdge, 46, 50);
        Assert.Equal(1, fastAgent.CurrentLane);
    }

    [Fact]
    public void ExploreLanesWithDifferentSpatialModalities()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            // NetworkMerge = true
        });
        var startNode = environment.AddNode();
        var goalNode = environment.AddNode();
        var edgeCar = environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 2 } }, SpatialModalityType.CarDriving);
        var edgeCycling = environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 1 } },
            new[] { SpatialModalityType.CarDriving, SpatialModalityType.Cycling });
        var edge = startNode.OutgoingEdges.First().Value;
        Assert.Equal(edgeCar, edge);
        Assert.Equal(edgeCycling, edge);
        Assert.Single(environment.Edges);
        Assert.Equal(3, edge.LaneCount);

        var car0 = new Car();
        Assert.True(environment.Insert(car0, edge, 30));
        var car1 = new Car();
        Assert.True(environment.Insert(car1, edge, 20, 1));

        var explore1 = edge.Explore(car1).LaneExplores;
        Assert.Equal(3, explore1.Count);
        Assert.NotEmpty(explore1[0].Forward);
        Assert.Empty(explore1[0].Backward);
        Assert.Empty(explore1[1].Forward);
        Assert.Empty(explore1[1].Backward);
        Assert.Empty(explore1[2].Forward);
        Assert.Empty(explore1[2].Backward);

        var bicycle = new Bicycle();
        Assert.True(environment.Insert(bicycle, edge, 15, 2));

        var explore2 = edge.Explore(car1).LaneExplores;
        Assert.Equal(3, explore2.Count);
        Assert.NotEmpty(explore2[0].Forward);
        Assert.Empty(explore2[0].Backward);
        Assert.Empty(explore2[1].Forward);
        Assert.Empty(explore2[1].Backward);
        Assert.Empty(explore2[2].Forward);
        Assert.NotEmpty(explore2[2].Backward);

        var exploreBicycle = edge.Explore(bicycle).LaneExplores;
        Assert.Equal(2, exploreBicycle.Count);
        Assert.NotEmpty(exploreBicycle[1].Forward);
        Assert.Empty(exploreBicycle[1].Backward);
        Assert.Empty(exploreBicycle[2].Forward);
        Assert.Empty(exploreBicycle[2].Backward);
    }

    [Fact]
    public void OvertakeOnLanesWithDifferentSpatialModalitiesPossible()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            // NetworkMerge = true
        });
        var startNode = environment.AddNode();
        var goalNode = environment.AddNode();
        environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 1 } }, SpatialModalityType.CarDriving);
        environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 1 } },
            new[] { SpatialModalityType.CarDriving, SpatialModalityType.Cycling });
        var edge = startNode.OutgoingEdges.First().Value;

        var car = new Car();
        Assert.True(environment.Insert(car, edge, 60));

        var agent = new OvertakingAgent(environment, startNode, goalNode, 50, 5);
        Assert.InRange(agent.PositionOnCurrentEdge, 50, 50);
        Assert.Equal(0, agent.CurrentLane);

        agent.Tick();
        agent.Tick();
        agent.Tick();
        Assert.InRange(agent.PositionOnCurrentEdge, 60, 70);
        Assert.Equal(1, agent.CurrentLane);
    }

    [Fact]
    public void OvertakeOnLanesWithDifferentSpatialModalitiesNotPossible()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            // NetworkMerge = true
        });
        var startNode = environment.AddNode();
        var goalNode = environment.AddNode();
        environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 1 } }, SpatialModalityType.CarDriving);
        environment.AddEdge(startNode, goalNode, 100,
            new Dictionary<string, object> { { "length", 100 }, { "lanes", 1 } }, SpatialModalityType.Cycling);
        var edge = startNode.OutgoingEdges.First().Value;

        var car = new Car();
        Assert.True(environment.Insert(car, edge, 60));

        var agent = new OvertakingAgent(environment, startNode, goalNode, 50, 5);
        Assert.InRange(agent.PositionOnCurrentEdge, 50, 50);
        Assert.Equal(0, agent.CurrentLane);

        agent.Tick();
        agent.Tick();
        agent.Tick();
        Assert.InRange(agent.PositionOnCurrentEdge, 50, 60);
        Assert.Equal(0, agent.CurrentLane);
    }
}

internal class OvertakingAgent : IAgent, ICarSteeringCapable
{
    private readonly CarSteeringHandle _steering;

    public OvertakingAgent(ISpatialGraphEnvironment environment, ISpatialNode startNode, ISpatialNode goalNode,
        double positionOnEdge, double maxSpeed, int laneOnEdge = 0)
    {
        var route = environment.FindShortestRoute(startNode, goalNode);

        Car = Golf.Create(environment);
        Car.IsCollidingEntity = true;
        Car.MaxSpeed = maxSpeed;
        Car.Velocity = maxSpeed;
        Car.Position = Car.CalculateNewPositionFor(route, out _);
        if (!environment.Insert(Car, startNode.OutgoingEdges.First().Value, positionOnEdge, laneOnEdge))
            throw new ApplicationException("The insertion in the environment was not possible");
        Assert.True(Car.TryEnterDriver(this, out _steering));
        Assert.NotNull(_steering);
        _steering.Route = route;
    }

    public double PositionOnCurrentEdge => Car.PositionOnCurrentEdge;
    public int CurrentLane => Car.LaneOnCurrentEdge;

    public Guid ID { get; set; }

    public void Tick()
    {
        _steering.Move();
    }

    public Position Position
    {
        get => Car.Position;
        set => throw new ApplicationException("Don't set Position of agent, it is set by steering handle.");
    }

    public void Notify(PassengerMessage passengerMessage)
    {
        throw new NotImplementedException();
    }

    public bool OvertakingActivated => true;

    public bool BrakingActivated
    {
        get => false;
        set { }
    }

    public bool CurrentlyCarDriving => true;
    public Car Car { get; }
}