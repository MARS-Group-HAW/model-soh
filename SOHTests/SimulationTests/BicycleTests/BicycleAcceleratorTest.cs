using System;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Common;
using SOHBicycleModel.Steering;
using SOHDomain.Steering.Common;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests
{
    /// <summary>
    ///     All tests are comparing the calculated results against manually calculated results of the
    ///     intelligent driver model. An excel sheet name 'idm_ideal_values' can be found in the
    ///     'analysis' folder of this repository that contains the values
    /// </summary>
    public class BicycleAcceleratorTest
    {
        private const double MaxSpeed = 4.167;
        private const double Speed = 2;
        private const double Accel = 2.0;
        private const double Gap2Pred = 5;
        private const double DriverRand = 0.5;
        private const double Gradient = 0;
        private const double Power = 75;
        private const double BicycleWeight = 15;
        private const double CyclistWeight = 70;
        private const double Weight = BicycleWeight + CyclistWeight;

        private double CalculateDecelerationAndAssert(double currentSpeed, double accel, double gap,
            double targetDeceleration, double targetSpeed)
        {
            var accelerator = new WiedemannAccelerator(new WiedemannCapable(Weight));
            // double driverSpefificFollowingVariation = HandleDriverType.DetermineFollowingVariation(DriverType);
            // double driverSpecificHeadwayTime        = HandleDriverType.DetermineHeadwayTime(DriverType);
            // accelerator.SetDriverSpecificParams();
            // TODO work with default params
            var deceleration = accelerator.CalculateSpeedChange(currentSpeed, 0, gap, 0, accel,
                MaxSpeed);
            var speed = currentSpeed + deceleration;

            Assert.Equal(targetDeceleration, Math.Round(deceleration, 3));
            Assert.Equal(targetSpeed, Math.Round(speed, 3));

            return deceleration;
        }

        [Fact]
        public void ApproachStandingVehicleTest()
        {
            var deceleration = CalculateDecelerationAndAssert(Speed, Accel, Gap2Pred, -0.392, 1.608);
            var vnew = Speed + deceleration;
            var gap = Gap2Pred - vnew;
            deceleration = CalculateDecelerationAndAssert(vnew, deceleration, gap, -0.37, 1.238);
            vnew += deceleration;
            gap -= vnew;
            deceleration = CalculateDecelerationAndAssert(vnew, deceleration, gap, -0.34, 0.898);
            vnew += deceleration;
            gap -= vnew;
            deceleration = CalculateDecelerationAndAssert(vnew, deceleration, gap, -0.297, 0.601);
            vnew += deceleration;
            gap -= vnew;
            deceleration = CalculateDecelerationAndAssert(vnew, deceleration, gap, -0.239, 0.362);
        }

        private class WiedemannCapable : IBicycleSteeringCapable
        {
            public WiedemannCapable(in double weight)
            {
                Mass = weight;
            }

            public Position Position { get; set; }

            public void Notify(PassengerMessage passengerMessage)
            {
                throw new NotImplementedException();
            }

            public double DriverRandom { get; }
            public DriverType DriverType { get; }
            public double CyclingPower { get; }
            public double Mass { get; }
            public double Gradient { get; } = 0;
            public bool OvertakingActivated { get; }
        }
    }
}