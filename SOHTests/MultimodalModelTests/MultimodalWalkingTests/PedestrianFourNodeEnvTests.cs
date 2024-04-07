using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Environment;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalWalkingTests;

public class PedestrianFourNodeEnvTests
{
    [Fact]
    public void GoalReachedByWalk()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = FourNodeGraphEnv.Node1Pos;
        var goal = FourNodeGraphEnv.Node4Pos;

        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.Walking
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        for (var tick = 0; tick < 1000 && !agent.GoalReached; tick++, layer.Context.UpdateStep())
        {
            agent.Tick();
            if (!agent.GoalReached) Assert.Equal(Whereabouts.Sidewalk, agent.Whereabouts);
        }

        Assert.True(agent.GoalReached);
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        Assert.Equal(goal, agent.Position);
    }

    [Fact]
    public void StartIsGoal()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var layer = new TestMultimodalLayer(fourNodeGraphEnv.GraphEnvironment);

        var start = fourNodeGraphEnv.Node2.Position;
        var goal = fourNodeGraphEnv.Node2.Position;

        var agent = new TestMultiCapableAgent
        {
            StartPosition = start,
            GoalPosition = goal,
            ModalChoice = ModalChoice.Walking
        };
        agent.Init(layer);

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        for (var tick = 0;
             tick < 1000 && !agent.GoalReached;
             tick++, layer.Context.UpdateStep()) agent.Tick();

        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        Assert.True(agent.GoalReached);
        Assert.Equal(goal, agent.Position);
    }
}