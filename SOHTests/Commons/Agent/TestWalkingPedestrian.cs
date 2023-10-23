using Mars.Interfaces.Environments;
using SOHMultimodalModel.Model;
using SOHTests.Commons.Layer;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Moves from start to goal position by foot.
/// </summary>
public class TestWalkingPedestrian : MultiCapableAgent<TestMultimodalLayer>
{
    public Position GoalPosition { get; set; }

    public override void Init(TestMultimodalLayer layer)
    {
        base.Init(layer);

        Gender = GenderType.Female;
        EnvironmentLayer = layer.SpatialGraphMediatorLayer;

        var environment = EnvironmentLayer.Environment;
        var route = environment.FindShortestRoute(environment.NearestNode(StartPosition),
            environment.NearestNode(GoalPosition),
            edge => edge.Modalities.Contains(SpatialModalityType.Walking));
        MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
    }

    public override void Tick()
    {
        Move();
    }
}