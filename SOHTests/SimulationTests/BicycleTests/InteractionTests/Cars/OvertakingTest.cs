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
    public class OvertakingTest
    {
        [Fact]
        public void TestWhatever()
        {
            var network = Path.Combine("res", "vector_data", "line.geojson");
            var graph = new SpatialGraphEnvironment(network);

            var context = SimulationContext.Start2020InSeconds;
            //            CarDriver carDriver = new CarDriver();
//            var driver =
//                Enumerable.Range(0, 10)
//                          .Select(i => new TestCyclist(i, 75, 80, 0.60, 2, 1, BicycleType.City, graph))
//                          .ToList();
            var driver = Enumerable.Range(0, 2)
                .Select(i => new ComplexCyclist(context, i, 75, 80, 0.60, graph)).ToList();

            driver[1].MaxSpeed = 9.333;
            driver[0].MaxSpeed = 1.667;
            const int ticks = 3600;

            for (var i = 0; i < ticks; i++, context.UpdateStep(1))
            {
                driver.Shuffle();
                foreach (var infiniteDriver in driver) infiniteDriver.Tick();

                // When there are at least two agents on the lane then we have to find another entity ahead
                if (driver.Count > 1)
                    Assert.All(driver, d =>
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

            TripsOutputAdapter.PrintTripResult(driver);
        }
    }
}