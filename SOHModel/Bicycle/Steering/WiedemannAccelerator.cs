using Mars.Common.Core.Random;
using Mars.Numerics.Statistics;
using SOHModel.Bicycle.Common;
using SOHModel.Domain.Steering.Acceleration;

namespace SOHModel.Bicycle.Steering;

public class WiedemannAccelerator : IVehicleAccelerator
{
    // CC1 is a factor that restricts the longitudinal oscillation of the vehicles in the simulation; it refers
    // to the distance increment beyond the safety distance. Small values of CC2 represent
    // that drivers are more aggressive in their car-following behavior, meaning that the driver
    // will speed up or slow down at a higher frequency

    private const double StandstillDistance = 0.2; // m, CC0
    private const double NegativeFollowingThreshold = -0.25; // CC4
    private const double PositiveFollowingThreshold = 0.25; // CC5

    private const double OscillationSpeedDependency = 1; // CC6

/*
        private const double StandstillAcceleration = 1.8; // m / s^2, CC8
*/
/*
        private const double AccelerationAtEighty = 0.01; // m / s^2, CC9
*/
    private const double MaxDecelFactor = -5;
    private const double Efficiency = 0.95;

    private const double MaxAccelerationFactor = 3;


    // private const double BreakingDelay = 2.56;
    private readonly IBicycleSteeringCapable _driver;
    private double _driverRand = 0.5;
    private double _enteringFollowingThreshold = -20; // CC3
    private double _followingVariation = 2; // m, CC2
    private double _headwayTime = 1.5; // s, CC1

    private double _oscillationAcceleration = 0.20; // m / s^2, CC7

    public WiedemannAccelerator(IBicycleSteeringCapable driver)
    {
        _driver = driver;

        var driverType = driver.DriverType;
        var driverSpecificFollowingVariation = HandleDriverType.DetermineFollowingVariation(driverType);
        var driverSpecificHeadwayTime = HandleDriverType.DetermineHeadwayTime(driverType);
        var driverSpecificEnterFollowingThreshold = HandleDriverType.DetermineEnterFolowingThreshold(driverType);
        var driverSpecificOscillationAcceleration =
            HandleDriverType.DetermineOscillationAcceleration(driverType);
        SetDriverSpecificParams(driverSpecificFollowingVariation, driverSpecificHeadwayTime,
            driverSpecificEnterFollowingThreshold, driverSpecificOscillationAcceleration, _driverRand);
    }

    public double CalculateSpeedChange(double currentSpeed, double maxSpeed, double distanceToVehicleAhead,
        double speedVehicleAhead)
    {
        throw new NotImplementedException();
    }

    private void SetDriverSpecificParams(double headwayTime, double followingVariation,
        double enterFollowingThreshold, double oscillationAcceleration, double driverRand)
    {
        _followingVariation = followingVariation;
        _headwayTime = headwayTime;
        _driverRand = driverRand;
        _enteringFollowingThreshold = enterFollowingThreshold;
        _oscillationAcceleration = oscillationAcceleration;
    }

    private double CalcMaxAcceleration(double power, double velocity, double maxSpeed)
    {
        if (_driver.Mass == 0)
            throw new ArgumentException(
                $"{nameof(IBicycleSteeringCapable.Mass)} of {nameof(IBicycleSteeringCapable)} cannot be 0");

        var speed = Math.Abs(velocity);
        var currentMaxAccelerationFactor =
            new FastGaussianDistribution(MaxAccelerationFactor, 0.3D).Next(RandomHelper.Random);
        var adjustedPower = power * Efficiency;
        var epsilon = adjustedPower / (_driver.Mass * currentMaxAccelerationFactor);
        var t1 = adjustedPower / _driver.Mass;
        var t2 = 1 / (speed + epsilon);
        var t3 = Math.Pow(speed, 2) / Math.Pow(maxSpeed, 3);
        var t4 = 0.0;
        if (_driver.Gradient > 0.0)
            t4 = BicycleConstants.G * (_driver.Gradient / 100);

        return t1 * (t2 - t3) - t4;
    }

    public double CalculateSpeedChange(double currentSpeed, double speedAhead, double distanceAhead,
        double accelerationAhead, double currentAcceleration, double maxSpeed)
    {
        var dx = distanceAhead + StandstillDistance;
        var dv = speedAhead - currentSpeed;
        double sdxc;
        if (speedAhead <= 0)
            sdxc = StandstillDistance;
        else
            // TODO Sumo: sdxc = StandstillDistance
            sdxc = StandstillDistance + _headwayTime * currentSpeed;

        var sdxo = _followingVariation + sdxc;
        // TODO Sumo: / 1000
        var sdv = OscillationSpeedDependency * dx * dx;

        var sdvo = sdv;
        // TODO Sumo: speed > 0

        var sdvc = speedAhead > 0 ? NegativeFollowingThreshold - sdv : 0;

        // TODO Sumo: predspeed > PositiveFollowingThreshold
        if (currentSpeed > PositiveFollowingThreshold) sdvo += PositiveFollowingThreshold;

        double acceleration = 0;
        if (dx <= sdxc && dv <= sdvo)
        {
            // TODO Sumo: predSpeed > 0
            if (currentSpeed > 0)
                if (dv < 0)
                {
                    acceleration = dx > StandstillDistance
                        ? Math.Min(accelerationAhead + dv * dv / (StandstillDistance - dx), currentAcceleration)
                        : Math.Min(accelerationAhead + 0.5 * (dv - sdvo), currentAcceleration);

                    if (acceleration > -_oscillationAcceleration)
                        acceleration = -_oscillationAcceleration;
                    else
                        acceleration = Math.Max(acceleration, MaxDecelFactor + 0.5 * Math.Sqrt(currentSpeed));


                    // TODO added because otherwise, there are cases where the new speed would get really
                    // small because the previous acceleration is used again
                    if (currentSpeed + acceleration < speedAhead) acceleration = dv - _driverRand;
                }
        }
        else if (dv < sdvc && dx < sdxo + _enteringFollowingThreshold * (dv - NegativeFollowingThreshold))
        {
            acceleration = 0.5 * dv * dv / (sdxc - dx - 0.1);
            // TODO Sumo: doesnt appear there
            acceleration = Math.Max(acceleration, MaxDecelFactor + Math.Sqrt(currentSpeed));
        }
        else if (dv < sdvo && dx < sdxo)
        {
            if (currentAcceleration <= 0)
            {
                acceleration = Math.Min(currentSpeed + currentAcceleration < speedAhead ? dv : currentAcceleration,
                    -_oscillationAcceleration);
                if (currentSpeed + acceleration < 0) acceleration = -currentSpeed;
            }
            else
            {
                acceleration = Math.Max(currentAcceleration, _oscillationAcceleration);
            }
        }
        else if (dx > sdxc)
        {
            var maxAcceleration = CalcMaxAcceleration(_driver.CyclingPower, currentSpeed, maxSpeed);
            acceleration = dx < sdxo ? Math.Min(dv * dv / (sdxo - dx), maxAcceleration) : maxAcceleration;
        }

        return acceleration;
    }
}