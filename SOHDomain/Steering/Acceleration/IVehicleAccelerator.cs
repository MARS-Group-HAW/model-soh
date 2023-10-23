namespace SOHDomain.Steering.Acceleration;

public interface IVehicleAccelerator
{
    double CalculateSpeedChange(double currentSpeed, double maxSpeed, double distanceToVehicleAhead,
        double speedVehicleAhead);
}