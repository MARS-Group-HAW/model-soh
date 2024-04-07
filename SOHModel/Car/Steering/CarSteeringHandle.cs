using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.Car.Steering;

public class CarSteeringHandle : VehicleSteeringHandle<ICarSteeringCapable, IPassengerCapable, CarSteeringHandle,
    CarPassengerHandle>
{
    public CarSteeringHandle(ISpatialGraphEnvironment environment, Model.Car car) :
        base(environment, car, car.MaxSpeed)
    {
    }
}