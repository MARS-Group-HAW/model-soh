using System.IO;
using System.Linq;
using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Interfaces;
using SOHMultimodalModel.Output.Trips;
using SOHTests.SimulationTests.BicycleTests.Helper;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests.InteractionTests.Cars
{
    public class DriveRingTest
    {
        [Fact]
        public void TestWhatever()
        {
            var network = Path.Combine("res", "networks", "ring_network.geojson");
            var graph = new SpatialGraphEnvironment(network);

            var context = SimulationContext.Start2020InSeconds;

            var driver = Enumerable.Range(0, 5)
                .Select(i => new InfiniteCyclist(context, i, 75, 80, 0.60, graph)).ToList();
            var driver2 = Enumerable.Range(0, 5)
                .Select(i => new InfiniteDriver(context, i, graph)).ToList();

            //driver[1].MaxSpeed = 9.333;
            driver[2].MaxSpeed = 3.667;
            const int ticks = 3600;

            for (var i = 0; i < ticks; i++, context.UpdateStep(1))
            {
                driver.Shuffle();
                foreach (var infiniteDriver in driver) infiniteDriver.Tick();

                driver2.Shuffle();
                foreach (var infiniteDriver in driver2) infiniteDriver.Tick();

                // When there are at least two agents on the lane then we have to find another entity ahead
                if (driver.Count > 1)
                    Assert.All(driver, d =>
                    {
                        Assert.NotNull(d.DriverAhead);
                        Assert.NotEqual(d, d.DriverAhead);
                    });

                if (driver2.Count > 1)
                    Assert.All(driver2, d =>
                    {
                        Assert.NotNull(d.DriverAhead);
                        Assert.NotEqual(d, d.DriverAhead);
                    });
            }

            Assert.All(driver, infiniteDriver =>
            {
                Assert.True(infiniteDriver.Speed >= 0);
                Assert.True(infiniteDriver.PositionOnCurrentEdge >= 0);
                Assert.NotNull(infiniteDriver.CurrentEdge);
                Assert.Equal(0, infiniteDriver.LaneOnCurrentEdge);
                Assert.NotNull(infiniteDriver.Position);
            });

            TripsOutputAdapter.FileNameSuffix = "bicyle";
            TripsOutputAdapter.PrintTripResult(driver);

            Assert.All(driver2, infiniteDriver =>
            {
                Assert.True(infiniteDriver.Speed >= 0);
                Assert.True(infiniteDriver.PositionOnCurrentEdge >= 0);
                Assert.NotNull(infiniteDriver.CurrentEdge);
                Assert.Equal(0, infiniteDriver.LaneOnCurrentEdge);
                Assert.NotNull(infiniteDriver.Position);
            });

            TripsOutputAdapter.FileNameSuffix = "car";
            TripsOutputAdapter.PrintTripResult(driver2);
        }
    }
}