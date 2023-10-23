using Mars.Interfaces.Environments;
using SOHCarModel.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;

namespace SOHCarModel.Steering;

public class CarSteeringHandle : VehicleSteeringHandle<ICarSteeringCapable, IPassengerCapable, CarSteeringHandle,
    CarPassengerHandle>
{
    public CarSteeringHandle(ISpatialGraphEnvironment environment, Car car) :
        base(environment, car, car.MaxSpeed)
    {
    }
}