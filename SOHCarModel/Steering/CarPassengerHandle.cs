using SOHCarModel.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;

namespace SOHCarModel.Steering;

/// <summary>
///     Provides the possibility to leave a car as driver or passenger.
/// </summary>
public class CarPassengerHandle : VehiclePassengerHandle<ICarSteeringCapable, IPassengerCapable, CarSteeringHandle,
    CarPassengerHandle>
{
    public CarPassengerHandle(Car car) : base(car)
    {
    }
}