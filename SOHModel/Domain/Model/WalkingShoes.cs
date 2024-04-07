using Mars.Interfaces.Environments;
using SOHModel.Domain.Graph;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Domain.Model;

/// <summary>
///     This is the physical representation of a walking agent within any environment.
/// </summary>
public class WalkingShoes : Vehicle<IWalkingCapable, IPassengerCapable, WalkingSteeringHandle, IPassengerHandle>
{
    private readonly ISpatialGraphLayer _spatialGraphLayer;

    public WalkingShoes(ISpatialGraphLayer spatialGraphLayer, double walkingSpeed, double runningSpeed)
    {
        _spatialGraphLayer = spatialGraphLayer;
        WalkingSpeed = walkingSpeed;
        RunningSpeed = runningSpeed;
        IsCollidingEntity = false;
        ModalityType = SpatialModalityType.Walking;
        SetWalking();
    }

    public double PreferredSpeed { get; set; }
    public double WalkingSpeed { get; }
    public double RunningSpeed { get; }

    public void SetWalking()
    {
        PreferredSpeed = WalkingSpeed;
    }

    public void SetRunning()
    {
        PreferredSpeed = RunningSpeed;
    }

    protected override IPassengerHandle CreatePassengerHandle()
    {
        throw new ApplicationException("The walking shoes has no additional passenger");
    }

    protected override WalkingSteeringHandle CreateSteeringHandle(IWalkingCapable driver)
    {
        return new WalkingSteeringHandle(driver, _spatialGraphLayer);
    }
}