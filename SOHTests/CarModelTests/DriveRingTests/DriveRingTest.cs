using System.Linq;
using Mars.Common.Core.Collections;
using Mars.Common.Core.Random;
using Mars.Components.Environments;
using Mars.Interfaces;
using SOHTests.Commons.Agent;
using Xunit;

namespace SOHTests.CarModelTests.DriveRingTests;

public class DriveRingTest
{
    [Fact]
    public void TestDriveOnRing()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var driver = Enumerable.Range(0, 10).Select(x => x * 5).Reverse()
            .Select(i => new InfiniteSteeringDriver(context, i, graph)).ToList();

        const int ticks = 360;
        for (var i = 0; i < ticks; i++, context.UpdateStep())
        {
            driver.ShuffleEnumerable(RandomHelper.Random);
            foreach (var infiniteDriver in driver)
                infiniteDriver.Tick();
        }

        Assert.All(driver, infiniteDriver =>
        {
            Assert.True(infiniteDriver.Velocity >= 0);
            Assert.True(infiniteDriver.PositionOnCurrentEdge >= 0);
            Assert.NotNull(infiniteDriver.CurrentEdge);
            Assert.Equal(0, infiniteDriver.LaneOnCurrentEdge);
            Assert.NotNull(infiniteDriver.Position);
        });
    }

    [Fact]
    public void DriveTwoRoundsAndDontSlowDownOnIntersection()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var driver = new InfiniteSteeringDriver(context, 0, graph);

        const int ticks = 100;
        for (var tick = 0; tick < ticks; tick++, context.UpdateStep())
        {
            driver.Tick();
            if (tick > 15) Assert.InRange(driver.Velocity, 10, 15);
        }
    }

    [Fact]
    public void VehicleStaysOnLaneWhenSwitchingEdges()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        const int preferredLane = 1;
        var fastDriver = new InfiniteSteeringDriver(context, 0, graph, preferredLane);

        for (var i = 0; i < 400; i++)
        {
            fastDriver.Tick();
            Assert.Equal(preferredLane, fastDriver.LaneOnCurrentEdge);
        }
    }
}