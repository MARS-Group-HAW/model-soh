using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Car.Steering;

/// <summary>
///     Provides the possibility to leave a car as driver or passenger.
/// </summary>
public class CarPassengerHandle : VehiclePassengerHandle<ICarSteeringCapable, IPassengerCapable, CarSteeringHandle,
    CarPassengerHandle>
{
    public CarPassengerHandle(Model.Car car) : base(car)
    {
    }
}