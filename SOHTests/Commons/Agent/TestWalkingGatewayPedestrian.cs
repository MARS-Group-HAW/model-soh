using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Moves from start to goal position by foot and can use gateways.
/// </summary>
public class TestWalkingGatewayPedestrian : TestMultiCapableAgent
{
    public TestWalkingGatewayPedestrian()
    {
        Gender = GenderType.Female;
    }

    public override void Init(TestMultimodalLayer layer)
    {
        base.Init(layer);

        var environment = layer.SpatialGraphMediatorLayer.Environment;
        var gateway = layer.GatewayLayer.Validate(StartPosition, GoalPosition).Item2;
        Assert.NotNull(gateway);

        var route = environment.FindShortestRoute(environment.NearestNode(StartPosition),
            environment.NearestNode(gateway),
            edge => edge.Modalities.Contains(SpatialModalityType.Walking));
        MultimodalRoute = new MultimodalRoute(route, ModalChoice.Walking);
    }

    public override void Tick()
    {
        Move();
    }
}