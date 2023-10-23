using SOHCarModel.Steering;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Multimodal;
using SOHTests.Commons.Layer;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Uses walking and driving to move. Uses only simple routes to move.
/// </summary>
public class TestDrivingPedestrian : MultiCapableAgent<IMultimodalLayer>, ICarSteeringCapable
{
    public new bool OvertakingActivated => false;
    public new bool CurrentlyCarDriving => Car?.Driver.Equals(this) ?? false;

    public override void Init(IMultimodalLayer layer)
    {
        base.Init(layer);

        if (layer is TestMultimodalLayer testMultimodalLayer)
            EnvironmentLayer = testMultimodalLayer.SpatialGraphMediatorLayer;
        Gender = GenderType.Male;
    }

    public override void Tick()
    {
        Move();
    }
}