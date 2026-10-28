using System.Collections.Generic;
using Mars.Interfaces.Environments;
using Moq;
using Xunit;
using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model;
using SOHModel.SemiTruck.Model.Driver.Services;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHTests
{
    public class SemiTruckFuelTests
    {
        [Fact]
        public void TestLinearStrategy()
        {
            var strategy = new LinearFuelConsumptionStrategy();
            var truck = new SemiTruck { FuelConsumptionPer100Km = 20 };
            
            // 10 km should use 2 units
            double consumedFuelAmount = strategy.CalculateFuelCarrierAmountUsed(truck, 10.0, 1.0, 0.0);
            Assert.Equal(2.0, consumedFuelAmount, 3);
            
            // Range estimate: 100 units / 20 units/100km = 500 km
            double range = strategy.EstimateRemainingRangeKm(truck, 100.0);
            Assert.Equal(500.0, range, 3);
        }

        [Fact]
        public void TestRoadLoadStrategy()
        {
            var strategy = new RoadLoadFuelConsumptionStrategy();
            var truck = new SemiTruck
            {
                Mass = 40000, // 40t
                Width = 2.5,
                Height = 4.0,
                DragCoefficient = 0.6,
                RollingResistance = 0.01,
                Tank2WheelEfficiency = 0.35,
                FuelCarrierType = FuelCarrierType.Fuel,
                Velocity = 22.22, // 80 km/h
                Acceleration = 0
            };

            // Energy for 1 second at 80 km/h (approx 22.22 m)
            double distanceKm = (22.22 * 1.0) / 1000.0;
            double consumedFuelAmount = strategy.CalculateFuelCarrierAmountUsed(truck, distanceKm, 1.0, 0.0);
            
            Assert.True(consumedFuelAmount > 0);
            
            // Check if incline increases consumption
            double consumedFuelAmountWithIncline = strategy.CalculateFuelCarrierAmountUsed(truck, distanceKm, 1.0, 5.0);
            Assert.True(consumedFuelAmountWithIncline > consumedFuelAmount);

            // 1 s at 22.22 m/s: P = (Crr*m*g + 0.5*rho*Cd*A*v^2) * v, fuel = P / eta * 1 s
            double force = 0.01 * 40000 * 9.81 + 0.5 * 1.225 * 0.6 * (2.5 * 4.0) * 22.22 * 22.22;
            double expectedJoules = force * 22.22 / 0.35;
            double expected = FuelCarrierEnergyConverter.FromJoules(expectedJoules, FuelCarrierType.Fuel);
            Assert.Equal(expected, consumedFuelAmount, 6);
        }

        [Fact]
        public void TestChargingPowerIsLimitedByWeakerSideAndTapers()
        {
            var truck = new SemiTruck { FuelCarrierType = FuelCarrierType.Battery, MaxChargePowerKw = 1000 };

            // station (400 kW default) is the bottleneck below the taper start
            Assert.Equal(400, ChargingProfile.PowerKw(truck, 0.5), 6);

            // vehicle is the bottleneck
            truck.MaxChargePowerKw = 150;
            Assert.Equal(150, ChargingProfile.PowerKw(truck, 0.5), 6);
            Assert.Equal(150, ChargingProfile.PowerKw(truck, 0.8), 6);

            // linear taper from 80 % to 10 % of the power at 100 %
            Assert.Equal(150 * 0.55, ChargingProfile.PowerKw(truck, 0.9), 6);
            Assert.Equal(15, ChargingProfile.PowerKw(truck, 1.0), 6);

            // explicit station power
            Assert.Equal(100, ChargingProfile.PowerKw(truck, 0.5, 100), 6);

            // liquid fuel does not taper
            var diesel = new SemiTruck { FuelCarrierType = FuelCarrierType.Fuel };
            Assert.Equal(30_000, ChargingProfile.PowerKw(diesel, 0.99), 6);
        }

        [Fact]
        public void TestBatteryChargesGraduallyToFull()
        {
            var truck = new SemiTruck { FuelCarrierType = FuelCarrierType.Battery, MaxFuelCarrierAmount = 150 };
            double amount = 50; // 33 %
            int seconds = 0;
            double previousAdded = double.MaxValue;
            bool taperSeen = false;

            while (amount < truck.MaxFuelCarrierAmount && seconds < 10_000)
            {
                double added = ChargingProfile.AmountAdded(truck, amount, 1.0);
                Assert.True(added > 0);
                if (added < previousAdded - 1e-12) taperSeen = true;
                Assert.True(added <= previousAdded + 1e-12); // power never increases while charging
                previousAdded = added;
                amount += added;
                seconds++;
            }

            Assert.True(taperSeen);
            Assert.Equal(150, amount, 6);
            // 70 kWh at 350 kW (12 min) + 30 kWh tapering from 350 to 35 kW (analytically 13.2 min)
            Assert.InRange(seconds / 60.0, 24.5, 26);
        }

        // Guards the reflection access to the internal MARS setter (see RouteDistanceHelper remarks)
        [Fact]
        public void TestRouteDistanceHelperDiscountsRemainingDistance()
        {
            var edge = new Mock<ISpatialEdge>();
            edge.Setup(e => e.Length).Returns(1000);
            var route = new Route();
            route.Add(edge.Object);
            route.Add(edge.Object);
            Assert.Equal(2000, route.RemainingRouteDistanceToGoal);

            RouteDistanceHelper.Discount(route, 400);
            Assert.Equal(1600, route.RemainingRouteDistanceToGoal);

            RouteDistanceHelper.Discount(route, 5000);
            Assert.Equal(0, route.RemainingRouteDistanceToGoal);
        }

        [Fact]
        public void TestGradeFromElevationProfile()
        {
            // 1000 m edge, flat for 500 m, then +10 m over the second half (2% there)
            double[] profile = { 100, 100, 100, 105, 110 };
            Assert.Equal(0.0, SpeedAdjuster.ComputeGradePercent(profile, 1000, 100), 3);
            Assert.Equal(2.0, SpeedAdjuster.ComputeGradePercent(profile, 1000, 900), 3);
            // Reversed profile = downhill, bumpy-but-level edge = ~0 instead of a constant tag value
            Assert.Equal(-2.0, SpeedAdjuster.ComputeGradePercent(new double[] { 110, 105, 100, 100, 100 }, 1000, 100), 3);
            Assert.Equal(0.0, SpeedAdjuster.ComputeGradePercent(new double[] { 0, 3, 0, 3, 0 }, 1000, 500), 3);
        }

        [Fact]
        public void TestParseElevationProfile()
        {
            var edge = new Mock<ISpatialEdge>();
            edge.Setup(e => e.Attributes).Returns(new Dictionary<string, object> { ["elevation"] = "[1.5, 2.5,3]" });
            Assert.Equal(new[] { 1.5, 2.5, 3.0 }, SpeedAdjuster.ParseElevationProfile(edge.Object));

            edge.Setup(e => e.Attributes).Returns(new Dictionary<string, object> { ["elevation"] = "[1.5,null]" });
            Assert.Null(SpeedAdjuster.ParseElevationProfile(edge.Object));
        }

        [Fact]
        public void TestInclineSpeedLimitRecoversAfterClimb()
        {
            var truck = new SemiTruck { Mass = 40000, Power = 400, MaxSpeed = 25, PositionOnCurrentEdge = 100 };
            var driver = new SemiTruckDriver { SemiTruck = truck };
            var adjuster = new SpeedAdjuster();
            var tracker = new FuelConsumptionTracker();

            ISpatialEdge Edge(double[] elevation)
            {
                var edge = new Mock<ISpatialEdge>();
                edge.Setup(e => e.Length).Returns(1000);
                edge.Setup(e => e.Attributes).Returns(new Dictionary<string, object>
                    { ["elevation"] = "[" + string.Join(",", elevation) + "]" });
                return edge.Object;
            }

            var climb = Edge(new double[] { 0, 30, 60, 90, 120 }); // ~12 % grade
            var flat = Edge(new double[] { 100, 100, 100, 100, 100 });

            adjuster.AdjustForWeather(driver, null);
            adjuster.AdjustForIncline(climb, truck, driver, tracker);
            Assert.True(truck.MaxSpeed < 25);

            adjuster.AdjustForWeather(driver, null);
            adjuster.AdjustForIncline(flat, truck, driver, tracker);
            Assert.Equal(25, truck.MaxSpeed);
        }
    }
}
