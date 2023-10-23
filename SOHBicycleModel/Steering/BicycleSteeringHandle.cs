using Mars.Interfaces.Environments;
using SOHBicycleModel.Model;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;

namespace SOHBicycleModel.Steering;

public class BicycleSteeringHandle : VehicleSteeringHandle<IBicycleSteeringCapable, IPassengerCapable,
    BicycleSteeringHandle, BicyclePassengerHandle>
{
    private readonly Bicycle _bicycle;

    public BicycleSteeringHandle(ISpatialGraphEnvironment environment, Bicycle bicycle,
        IBicycleSteeringCapable driver) : base(environment, bicycle)
    {
        _bicycle = bicycle;
        VehicleAccelerator = new WiedemannAccelerator(driver);
    }

    private WiedemannAccelerator WiedemannAccelerator => (WiedemannAccelerator)VehicleAccelerator;

    protected override double CalculateSpeedChange(double currentSpeed, double maxSpeed,
        double distanceToVehicleAhead,
        double speedVehicleAhead, double accelerationVehicleAhead)
    {
        return WiedemannAccelerator.CalculateSpeedChange(currentSpeed,
            speedVehicleAhead, distanceToVehicleAhead,
            accelerationVehicleAhead, _bicycle.Acceleration,
            maxSpeed);
    }
}