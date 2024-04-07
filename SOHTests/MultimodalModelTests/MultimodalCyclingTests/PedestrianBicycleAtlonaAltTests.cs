using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalCyclingTests;

public class PedestrianBicycleAltonaAltstadtTests
{
    private readonly TestMultimodalLayer _layer;

    public PedestrianBicycleAltonaAltstadtTests()
    {
        var graph =
            new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.WalkGraphAltonaAltstadt,
                        InputConfiguration = new InputConfiguration { IsBiDirectedImport = true }
                    }
                }
            });

        _layer = new TestMultimodalLayer(graph)
        {
            BicycleRentalLayer = new BicycleRentalLayerFixture(graph).BicycleRentalLayer
        };
    }

    private (Position start, Position goal) FindReasonableStartAndGoal()
    {
        var start = _layer.StreetEnvironment.GetRandomNode().Position;
        var goal = _layer.StreetEnvironment.GetRandomNode().Position;
        var counter = 0;
        while (start.DistanceInMTo(goal) < 2000)
        {
            goal = _layer.StreetEnvironment.GetRandomNode().Position;
            if (counter++ > 100)
                //find new start
                return FindReasonableStartAndGoal();
        }

        return (start, goal);
    }

    [Fact]
    public void CycleWalkManyAgents()
    {
        var agents = new List<TestMultiCapableAgent>();
        const int agentCount = 30;
        for (var i = 0; i < agentCount; i++)
        {
            var (start, goal) = FindReasonableStartAndGoal();
            var agent = new TestMultiCapableAgent
            {
                StartPosition = start,
                GoalPosition = goal,
                ModalChoice = ModalChoice.CyclingRentalBike
            };
            agent.Init(_layer);
            agents.Add(agent);
        }

        Assert.All(agents, a => Assert.NotNull(a.MultimodalRoute));
        Assert.All(agents, a => Assert.Equal(ModalChoice.CyclingRentalBike, a.RouteMainModalChoice));

        const int ticks = 3000;
        for (var tick = 0; tick <= ticks; tick++, _layer.Context.UpdateStep())
            foreach (var agent in agents)
                agent.Tick();

        Assert.All(agents, a => Assert.True(a.GoalReached));
        Assert.All(agents, a => Assert.True(a.HasUsedBicycle));
        Assert.All(agents,
            a =>
            {
                var distanceToGoal =
                    a.Position.DistanceInMTo(a.MultimodalRoute.Stops.Last().Route.Stops.Last().Edge.To.Position);
                Assert.InRange(distanceToGoal, 0, 15);
            });

        Assert.All(agents, a => Assert.Null(a.RentalBicycle));
        Assert.All(agents, a => Assert.Equal(Whereabouts.Offside, a.Whereabouts));
    }

    [Fact]
    public void WalkCycleWalkSpecificRoute()
    {
        var start = Position.CreateGeoPosition(9.9423436, 53.5488809);
        var goal = Position.CreateGeoPosition(9.9277651, 53.5446598);
        // var start = Position.CreateGeoPosition(9.93520213357434, 53.549863306078855);
        // var goal = Position.CreateGeoPosition(9.9474231, 53.5461301);

        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.CyclingRentalBike
        };
        agent.Init(_layer);
        Assert.Equal(ModalChoice.CyclingRentalBike, agent.RouteMainModalChoice);

        for (var tick = 0; tick <= 5000 && !agent.GoalReached; tick++, _layer.Context.UpdateStep()) agent.Tick();

        Assert.True(agent.GoalReached);
    }
}