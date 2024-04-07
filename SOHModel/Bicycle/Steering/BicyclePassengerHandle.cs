using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Bicycle.Steering;

/// <summary>
///     Provides the possibility to leave a bicycle as driver or passenger.
/// </summary>
public class BicyclePassengerHandle : VehiclePassengerHandle<IBicycleSteeringCapable, IPassengerCapable,
    BicycleSteeringHandle, BicyclePassengerHandle>
{
    public BicyclePassengerHandle(Model.Bicycle bicycle) : base(bicycle)
    {
    }
}