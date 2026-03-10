using Xunit;
using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model;

namespace SOHTests
{
    public class SemiTruckFuelTests
    {
        [Fact]
        public void TestLinearStrategy()
        {
            var strategy = new LinearFuelConsumptionStrategy();
            var truck = new SemiTruck { EnergyConsumptionPer100Km = 20 };
            
            // 10 km should use 2 units
            double energyUsed = strategy.CalculateEnergyUsed(truck, 10.0, 1.0, 0.0);
            Assert.Equal(2.0, energyUsed, 3);
            
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
                EnergyType = EnergyType.Fuel,
                Velocity = 22.22, // 80 km/h
                Acceleration = 0
            };

            // Energy for 1 second at 80 km/h (approx 22.22 m)
            double distanceKm = (22.22 * 1.0) / 1000.0;
            double energyUsed = strategy.CalculateEnergyUsed(truck, distanceKm, 1.0, 0.0);
            
            Assert.True(energyUsed > 0);
            
            // Check if incline increases consumption
            double energyUsedWithIncline = strategy.CalculateEnergyUsed(truck, distanceKm, 1.0, 5.0);
            Assert.True(energyUsedWithIncline > energyUsed);
        }
    }
}
