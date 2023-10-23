using Mars.Interfaces.Environments;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Uses walking and driving to move along multimodal routes.
/// </summary>
public class TestMultiCapableCarDriver : TestMultiCapableAgent
{
    public override void Init(TestMultimodalLayer layer)
    {
        base.Init(layer);

        Assert.NotNull(StartPosition);
        Assert.NotNull(GoalPosition);
        Assert.NotNull(Car);
        Assert.NotNull(Car.CarParkingLayer);
        MultimodalRoute = layer.Search(this, StartPosition, GoalPosition, ModalChoice.CarDriving);

        if (!StartPosition.Equals(GoalPosition)) Assert.NotEmpty(MultimodalRoute);
    }
}