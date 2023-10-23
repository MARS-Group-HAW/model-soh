using SOHBicycleModel.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;

namespace SOHBicycleModel.Steering;

/// <summary>
///     Provides the possibility to leave a bicycle as driver or passenger.
/// </summary>
public class BicyclePassengerHandle : VehiclePassengerHandle<IBicycleSteeringCapable, IPassengerCapable,
    BicycleSteeringHandle, BicyclePassengerHandle>
{
    public BicyclePassengerHandle(Bicycle bicycle) : base(bicycle)
    {
    }
}